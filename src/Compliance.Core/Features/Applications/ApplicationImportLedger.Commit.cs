using Cntryl.Portia;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;

namespace Bdgrz.Compliance.Features.Applications;

public sealed partial class ApplicationImportLedger
{
    readonly Dictionary<Uuid, ApplicationImportCommitted> _commits = [];
    readonly Dictionary<Uuid, ulong> _commitVersions = [];

    public ApplicationImportCommitted? GetCommit(Uuid batchId) => _commits.GetValueOrDefault(batchId);

    public bool IsCommitDurable(Uuid batchId) => _commitVersions.TryGetValue(batchId, out var version) &&
        CommittedStreamPosition >= version;

    public bool IsEffectCommitted(ApplicationImportEffectPending effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        if (effect.TenantId != _tenantId || effect.Plan.SourceKey != _sourceKey || effect.Plan.SourceNamespace != _sourceNamespace ||
            !IsCommitDurable(effect.Plan.BatchId) || !_frozenPlans.TryGetValue(effect.Plan.BatchId, out var plan))
            return false;
        var row = plan.Rows.SingleOrDefault(row => row.RowId == effect.Row.RowId);
        return row is not null && effect == new ApplicationImportEffectPending(_tenantId, row.ApplicationId,
                   plan.Revision, plan.PlanSha256, plan.Start, row, effect.RecordedAt) &&
               _commits[effect.Plan.BatchId].Effects.Any(proof => proof.RowId == row.RowId && proof.ApplicationId == effect.ApplicationId &&
                   proof.EventId == effect.Metadata.EventId && proof.EventVersion == effect.Metadata.AggregateVersion);
    }

    public Result Commit(ImportBatch batch, long expectedRevision,
        IReadOnlyDictionary<Uuid, DeclaredApplication> targets, DateTimeOffset committedAt)
        => Commit(batch, expectedRevision, targets, committedAt, null);

    public Result Commit(ImportBatch batch, long expectedRevision,
        IReadOnlyDictionary<Uuid, DeclaredApplication> targets, DateTimeOffset committedAt,
        IReadOnlyDictionary<Uuid, ApplicationChangePreview>? retirementImpacts)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(targets);
        if (!BelongsToSource(batch))
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The import batch was not found for this source."));
        if (_commits.TryGetValue(batch.Id, out var existing))
            return expectedRevision == existing.Revision || expectedRevision == existing.Revision - 1 ? Result.Success :
                Result.Failure(VersionedRecordRules.StaleRevision("import", GetRevision(batch)).ToRequestError());
        var verified = VerifyPendingEffects(batch, expectedRevision, targets, retirementImpacts);
        if (!verified.IsSuccess)
            return Result.Failure(verified.Error);
        var plan = _frozenPlans[batch.Id];
        var proofs = Array.AsReadOnly(verified.Value.Select(effect => new ApplicationImportEffectProof(
            effect.Row.RowId, effect.ApplicationId, effect.Metadata.EventId, effect.Metadata.AggregateVersion)).ToArray());
        var marker = new ApplicationImportCommitted(_tenantId, _sourceKey, _sourceNamespace, batch.Id,
            expectedRevision + 1, plan.PlanSha256, proofs, committedAt);
        if (JsonSerializer.SerializeToUtf8Bytes(marker, ComplianceCoreJsonContext.Default.ApplicationImportCommitted).Length > ImportBatch.MaximumStagedPayloadBytes)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The import commit exceeds its event payload bounds."));
        RaiseEvent(marker);
        return Result.Success;
    }

    void RegisterCommitEvents() => On<ApplicationImportCommitted>(ev =>
    {
        if (ev.TenantId != _tenantId || ev.SourceKey != _sourceKey || ev.SourceNamespace != _sourceNamespace ||
            _acceptingBatchId != ev.BatchId || _commits.ContainsKey(ev.BatchId) || _canceledRevisions.ContainsKey(ev.BatchId) ||
            !_frozenPlans.TryGetValue(ev.BatchId, out var plan) || ev.Revision != _revisions[ev.BatchId] + 1 ||
            ev.PlanSha256 != plan.PlanSha256 || ev.Effects.Count != plan.Rows.Count ||
            ev.Effects.Select(proof => proof.RowId).Distinct().Count() != plan.Rows.Count ||
            ev.Effects.Select(proof => proof.EventId).Distinct().Count() != plan.Rows.Count ||
            ev.Effects.Select(proof => (proof.ApplicationId, proof.EventVersion)).Distinct().Count() != plan.Rows.Count ||
            ev.Effects.Any(proof => proof.EventId == Uuid.Empty || proof.EventVersion == 0 ||
                !plan.Rows.Any(row => row.RowId == proof.RowId && row.ApplicationId == proof.ApplicationId)))
            throw new InvalidOperationException("The import commit does not match its complete active frozen plan.");
        _commits.Add(ev.BatchId, ev with { Effects = Array.AsReadOnly(ev.Effects.ToArray()) });
        _commitVersions.Add(ev.BatchId, ev.Metadata.AggregateVersion);
        _revisions[ev.BatchId] = ev.Revision;
        _acceptingBatchId = null;
    });
}
