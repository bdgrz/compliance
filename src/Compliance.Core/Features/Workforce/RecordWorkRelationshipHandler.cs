using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class RecordWorkRelationshipHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<RecordWorkRelationship, WorkRelationshipRegistration>
{
    public async ValueTask<Result<WorkRelationshipRegistration>> HandleAsync(
        IRequestContext<RecordWorkRelationship> context, CancellationToken ct)
    {
        var actor = WorkforceActor.From(context);
        var request = context.Request;
        var terms = new WorkRelationshipTerms(request.WorkerType, request.LifecycleStatus,
            request.StartDate, request.EndDate, request.Department, request.ManagerPersonId,
            request.SponsorPersonId);
        var invalid = await WorkRelationshipReferences.ValidateAsync(reader, request.TenantId,
            request.PersonId, terms, ct).ConfigureAwait(false);
        if (invalid is not null)
            return Result<WorkRelationshipRegistration>.Failure(invalid);
        var relationshipId = WorkRelationship.IdFor(request.TenantId, request.SourceWorkerId ?? "");
        return await executor.ExecuteAsync(new WorkRelationship(request.TenantId, relationshipId),
            relationship => AggregateOutcome.CommitOnSuccess(relationship.Record(request.PersonId,
                request.SourceWorkerId ?? "", terms, actor, clock.GetUtcNow())), context, ct)
            .ConfigureAwait(false);
    }
}
