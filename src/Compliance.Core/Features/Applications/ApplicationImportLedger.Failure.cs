using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed partial class ApplicationImportLedger
{
    readonly Dictionary<Uuid, ApplicationImportFailed> _failures = [];
    readonly Dictionary<Uuid, ulong> _failureVersions = [];

    public ApplicationImportFailed? GetFailure(Uuid batchId) => _failures.GetValueOrDefault(batchId);

    public bool IsRollbackDurable(Uuid batchId) => IsCancellationDurable(batchId) ||
        (_failureVersions.TryGetValue(batchId, out var version) && version > 0 && CommittedStreamPosition >= version);

    public Result Fail(ImportBatch batch, string planSha256, Uuid? rowId, string failureCode, DateTimeOffset failedAt)
    {
        if (!BelongsToSource(batch))
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The import batch was not found."));
        if (GetState(batch) is "canceled" or "committed" or "failed")
            return Result.Success;
        if (_acceptingBatchId != batch.Id || !_frozenPlans.TryGetValue(batch.Id, out var plan) ||
            plan.PlanSha256 != planSha256 || (rowId is { } id && !plan.Rows.Any(row => row.RowId == id)))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The failure does not belong to the active import plan."));
        if (failureCode is not ("target_effect_rejected" or "commit_verification_rejected"))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The import failure code is invalid."));
        RaiseEvent(new ApplicationImportFailed(_tenantId, _sourceKey, _sourceNamespace, batch.Id,
            GetRevision(batch) + 1, planSha256, rowId, failureCode, failedAt));
        return Result.Success;
    }

    void RegisterFailureEvents() => On<ApplicationImportFailed>(ev =>
    {
        if (ev.TenantId != _tenantId || ev.SourceKey != _sourceKey || ev.SourceNamespace != _sourceNamespace ||
            _acceptingBatchId != ev.BatchId || _failures.ContainsKey(ev.BatchId) || _commits.ContainsKey(ev.BatchId) ||
            _canceledRevisions.ContainsKey(ev.BatchId) || !_frozenPlans.TryGetValue(ev.BatchId, out var plan) ||
            ev.PlanSha256 != plan.PlanSha256 || ev.Revision != _revisions[ev.BatchId] + 1 ||
            (ev.RowId is { } id && !plan.Rows.Any(row => row.RowId == id)) ||
            ev.FailureCode is not ("target_effect_rejected" or "commit_verification_rejected"))
            throw new InvalidOperationException("The import failure does not match its active frozen plan.");
        _failures.Add(ev.BatchId, ev);
        _failureVersions.Add(ev.BatchId, ev.Metadata.AggregateVersion);
        _revisions[ev.BatchId] = ev.Revision;
        _acceptingBatchId = null;
    });
}
