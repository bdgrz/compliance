using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Records an explicit classification against governed workforce identities. The provider
///     hint shown at the time is retained beside the decision; it never decides.
/// </summary>
public sealed class ClassifyAccessPrincipalHandler(IAggregateExecutor executor,
    IAggregateReader reader, IAccessReviewSources sources, TimeProvider clock)
    : IRequestHandler<ClassifyAccessPrincipal, AccessPrincipalClassificationView>
{
    public async ValueTask<Result<AccessPrincipalClassificationView>> HandleAsync(
        IRequestContext<ClassifyAccessPrincipal> context, CancellationToken ct)
    {
        var request = context.Request;
        foreach (var personId in new[] { request.PersonId, request.AccountableOwnerPersonId })
        {
            if (personId is { } id &&
                !(await reader.HydrateAsync(new Person(request.TenantId, id), ct).ConfigureAwait(false)).IsCreated)
                return AccessReviewOutcome.Failure<AccessPrincipalClassificationView>(
                    RequestErrorKind.Validation, "The person must be a recorded workforce person in this tenant.");
        }
        if (request.ServiceIdentityId is { } serviceIdentityId &&
            !(await reader.HydrateAsync(new ServiceIdentity(request.TenantId, serviceIdentityId), ct)
                .ConfigureAwait(false)).IsCreated)
            return AccessReviewOutcome.Failure<AccessPrincipalClassificationView>(
                RequestErrorKind.Validation, "The service identity must be governed in this tenant.");
        var population = await reader.HydrateAsync(new AccessPopulation(request.TenantId,
            request.PopulationId), ct).ConfigureAwait(false);
        if (!population.IsOpened)
            return AccessReviewOutcome.Failure<AccessPrincipalClassificationView>(
                RequestErrorKind.NotFound, "The population was not found.");
        string? proposed = null;
        if (population.Facts.Principals.FirstOrDefault(principal =>
                principal.ProviderSubjectId == request.ProviderSubjectId) is { } fact)
        {
            var people = await sources.ListPeopleAsync(request.TenantId, ct).ConfigureAwait(false);
            if (!people.IsSuccess)
                return Result<AccessPrincipalClassificationView>.Failure(people.Error);
            proposed = AccessPrincipalProposals.Propose(fact, people.Value).Proposal?.Classification;
        }
        var actor = AccessReviewActor.From(context);
        return await executor.ExecuteAsync(new AccessPopulation(request.TenantId, request.PopulationId),
            accepted => AccessReviewOutcome.From(accepted.Classify(request.ProviderSubjectId,
                request.ExpectedClassificationCount, request.Classification, request.Rationale,
                request.PersonId, request.ServiceIdentityId, request.AccountableOwnerPersonId,
                request.SharedJustification, proposed, actor.Reference, clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
