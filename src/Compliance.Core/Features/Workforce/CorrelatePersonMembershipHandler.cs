using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     Records an attributable person-to-member correlation. Only tenant client personnel can be
///     correlated; nothing here changes roles, grants, or membership state.
/// </summary>
public sealed class CorrelatePersonMembershipHandler(IAggregateExecutor executor,
    ITenantMembershipDirectoryReader memberships, TimeProvider clock)
    : IRequestHandler<CorrelatePersonMembership>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<CorrelatePersonMembership> context,
        CancellationToken ct)
    {
        var actor = WorkforceActor.From(context);
        var request = context.Request;
        if (request.UserId is { } userId)
        {
            var membership = await memberships.GetAsync(request.TenantId.ToString(), userId, ct)
                .ConfigureAwait(false);
            if (membership is null || membership.TenantId != request.TenantId)
                return Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "The platform member was not found in the tenant."));
            if (membership.Affiliation == "firm_staff")
                return Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "Firm staff are not tenant workforce and cannot be correlated to the roster."));
        }
        return await executor.ExecuteAsync(new Person(request.TenantId, request.PersonId),
            person => CommandFailureRequestAdapter.ToOutcome(person.CorrelateMembership(
                request.ExpectedRevision, request.UserId, actor, clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
