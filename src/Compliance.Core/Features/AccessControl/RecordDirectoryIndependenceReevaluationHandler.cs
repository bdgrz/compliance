using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class RecordDirectoryIndependenceReevaluationHandler(IDomainEventReader events,
    IAggregateReader reader, IAggregateExecutor executor, ITenantActivity tenants, TimeProvider clock)
    : IRequestHandler<RecordDirectoryIndependenceReevaluation>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RecordDirectoryIndependenceReevaluation> context, CancellationToken ct)
    {
        var process = RecordDirectoryIndependenceReevaluationAuthorizer.Process(context);
        if (process is null)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the named internal reconciliation workers may record directory receipts."));
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.StaffMemberId == Uuid.Empty || request.UserId == Uuid.Empty)
            return RefuseSource();
        var directory = await reader.HydrateAsync(new FirmStaffDirectory(), ct).ConfigureAwait(false);
        var directoryPosition = directory.CommittedStreamPosition;
        var client = await reader.HydrateAsync(new IndependenceLedger(request.TenantId), ct).ConfigureAwait(false);
        var clientPosition = client.CommittedStreamPosition;
        if (request.DirectoryResourceOffset >= directoryPosition || request.AcceptanceResourceOffset >= clientPosition)
            return RefuseSource();
        var directoryRecord = await ReadRecordAsync(new FirmStaffDirectory().Stream, request.DirectoryResourceOffset, ct).ConfigureAwait(false);
        var acceptedRecord = await ReadRecordAsync(new IndependenceLedger(request.TenantId).Stream,
            request.AcceptanceResourceOffset, ct).ConfigureAwait(false);
        if (directoryRecord?.Event is not FirmStaffChangeRecorded change ||
            acceptedRecord?.Event is not ServiceEngagementAcceptanceRecorded original ||
            directoryRecord.Event.Metadata.EventId != request.DirectoryEventId ||
            acceptedRecord.Event.Metadata.EventId != request.AcceptanceEventId ||
            original.RequestId != request.AcceptanceRequestId || original.TenantId != request.TenantId ||
            IndependenceSourceDigest.DirectoryEvent(change) != request.DirectoryPayloadSha256 ||
            IndependenceSourceDigest.AcceptanceEvent(original) != request.AcceptancePayloadSha256 ||
            change.ExpectedSequence is < 0 or long.MaxValue ||
            change.Staff.StaffMemberId != request.StaffMemberId || change.Staff.UserId != request.UserId ||
            context.CausationId != (process == "reactor:FirmStaffStatusReevaluationV1"
                ? request.DirectoryEventId : request.AcceptanceEventId))
            return RefuseSource();
        if (!directory.MatchesRetainedStatus(change) || !client.MatchesAcceptanceSource(original))
            return RefuseSource();
        if (!await tenants.IsActiveAsync(request.TenantId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The owning client is inactive; reconciliation remains pending.", isTransient: true));
        var source = new DirectoryStatusSourceView(request.DirectoryEventId, change.RequestId,
            change.ExpectedSequence + 1, directoryRecord.ResourceOffset, request.DirectoryPayloadSha256,
            change.Staff.StaffMemberId, change.Staff.UserId, change.Staff.Practice,
            change.Staff.Revision, change.Staff.IsActive, change.Staff.RecordedAt);
        return await executor.ExecuteAsync(new IndependenceLedger(request.TenantId), ledger =>
        {
            var result = ledger.RecordDirectoryReevaluation(source, original,
                ActorReference.ForSystemProcess(process, process["reactor:".Length..]), clock.GetUtcNow());
            return AggregateOutcome.CommitOnSuccess(result.IsSuccess ? Result.Success : Result.Failure(result.Error));
        }, context, ct).ConfigureAwait(false);
    }

    async ValueTask<DomainEventRecord?> ReadRecordAsync(EventStreamAddress stream, ulong offset, CancellationToken ct)
    {
        await foreach (var record in events.ReadAsync(stream, offset, ct).WithCancellation(ct).ConfigureAwait(false))
            return record.Stream == stream && record.ResourceOffset == offset ? record : null;
        return null;
    }

    static Result RefuseSource() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The directory receipt requires its exact authoritative cause and original client acceptance."));
}
