using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Reads governed roster, service-identity, and application facts through their consistency guards.</summary>
public sealed class GovernedAccessReviewSources(IPersonDirectoryReader people,
    PersonReadConsistency peopleConsistency, IWorkforceObservationDirectoryReader roster,
    WorkforceObservationReadConsistency rosterConsistency,
    ServiceIdentityReadConsistency serviceIdentities, ServiceIdentityOwnership ownership,
    IApplicationDirectoryReader applications, IAggregateReader reader) : IAccessReviewSources
{
    const int MaximumPeople = 5000;

    public async ValueTask<Result<IReadOnlyList<PersonView>>> ListPeopleAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        var ready = await peopleConsistency.EnsureListCaughtUpAsync(tenantId, ct).ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<IReadOnlyList<PersonView>>.Failure(ready.Error);
        var items = new List<PersonView>();
        string? cursor = null;
        do
        {
            var page = await people.ListAsync(tenantId, 200, cursor, ct).ConfigureAwait(false);
            items.AddRange(page.Items.Where(person => person.TenantId == tenantId));
            cursor = page.NextCursor;
        } while (cursor is not null && items.Count < MaximumPeople);
        return Result<IReadOnlyList<PersonView>>.Success(items);
    }

    public async ValueTask<Result<IReadOnlyList<string>>> RelationshipStatusesAsync(Uuid tenantId,
        Uuid personId, CancellationToken ct = default)
    {
        var ready = await rosterConsistency.EnsureCaughtUpAsync(tenantId, ct).ConfigureAwait(false);
        return ready.IsSuccess
            ? Result<IReadOnlyList<string>>.Success(await roster.ListRelationshipStatusesAsync(
                tenantId, personId, ct).ConfigureAwait(false))
            : Result<IReadOnlyList<string>>.Failure(ready.Error);
    }

    public async ValueTask<Result<ServiceIdentityView>> GetServiceIdentityAsync(Uuid tenantId,
        Uuid serviceIdentityId, CancellationToken ct = default)
    {
        var view = await serviceIdentities.GetAsync(tenantId, serviceIdentityId, null, ct)
            .ConfigureAwait(false);
        if (!view.IsSuccess)
            return view;
        var evaluated = await ownership.EvaluateAsync(tenantId, [view.Value], ct).ConfigureAwait(false);
        return evaluated.IsSuccess
            ? Result<ServiceIdentityView>.Success(evaluated.Value[0])
            : Result<ServiceIdentityView>.Failure(evaluated.Error);
    }

    public async ValueTask<Result<Uuid?>> AccessOwnerMemberAsync(Uuid tenantId, Uuid applicationId,
        CancellationToken ct = default)
    {
        var source = await reader.HydrateApplicationAsync(tenantId, applicationId, ct)
            .ConfigureAwait(false);
        var application = await applications.GetAsync(tenantId, applicationId, ct).ConfigureAwait(false);
        if (!source.IsCreated || (application is not null &&
            (application.TenantId != tenantId || application.ApplicationId != applicationId)))
            return Result<Uuid?>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The application was not found."));
        if (application is null || application.Revision < source.Revision)
            return Result<Uuid?>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The application projection has not reached the source revision.", isTransient: true));
        if (application.AccessOwnerPersonId is not { } ownerId)
            return Result<Uuid?>.Success(null);
        var owner = await reader.HydrateAsync(new Person(tenantId, ownerId), ct).ConfigureAwait(false);
        return Result<Uuid?>.Success(owner.CorrelatedUserId is { } userId
            ? RbacIds.Member(tenantId, userId)
            : null);
    }
}
