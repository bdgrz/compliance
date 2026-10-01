using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class RecordWorkRelationshipHandler(IAggregateExecutor executor,
    IAggregateReader reader, IPermissionAuthorizer permissions, TimeProvider clock)
    : IRequestHandler<RecordWorkRelationship, WorkRelationshipRegistration>
{
    public async ValueTask<Result<WorkRelationshipRegistration>> HandleAsync(
        IRequestContext<RecordWorkRelationship> context, CancellationToken ct)
    {
        var actor = WorkforceActor.From(context);
        var request = context.Request;
        var terms = new WorkRelationshipTerms(request.WorkerType, request.LifecycleStatus,
            request.StartDate, request.EndDate, request.Department, request.ManagerPersonId,
            request.SponsorPersonId, request.EmploymentStatusReason);
        var invalid = await WorkRelationshipReferences.ValidateAsync(reader, request.TenantId,
            request.PersonId, terms, ct).ConfigureAwait(false);
        if (invalid is not null)
            return Result<WorkRelationshipRegistration>.Failure(invalid);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("WorkforceAuthorizer must reject this actor.");
        var managerChain = await FieldRestrictions.ForActorAsync(permissions, request.TenantId,
            userId, FieldClasses.WorkforceManagerChain, ct).ConfigureAwait(false);
        var personalDetails = await FieldRestrictions.ForActorAsync(permissions, request.TenantId,
            userId, FieldClasses.WorkforcePersonalDetails, ct).ConfigureAwait(false);
        var relationshipId = WorkRelationship.IdFor(request.TenantId, request.SourceWorkerId ?? "");
        return await executor.ExecuteAsync(new WorkRelationship(request.TenantId, relationshipId),
            relationship =>
            {
                // Decide from the executor's hydrated state so a concurrent first record cannot
                // bypass this gate through an earlier missing-record read.
                if (relationship.IsCreated && (!managerChain.CanRead || !personalDetails.CanRead))
                    return AggregateOutcome.Discard(Result<WorkRelationshipRegistration>.Failure(
                        new RequestError(RequestErrorKind.Forbidden,
                            "Retrying a work relationship requires both restricted workforce field grants.")));
                return AggregateOutcome.CommitOnSuccess(relationship.Record(request.PersonId,
                    request.SourceWorkerId ?? "", terms, actor, clock.GetUtcNow()));
            }, context, ct)
            .ConfigureAwait(false);
    }
}
