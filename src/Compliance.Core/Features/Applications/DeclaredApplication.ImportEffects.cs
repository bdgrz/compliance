using Cntryl.Portia;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;

namespace Bdgrz.Compliance.Features.Applications;

public sealed partial class DeclaredApplication
{
    readonly Dictionary<(Uuid BatchId, Uuid RowId), ApplicationImportEffectPending> _pendingImportEffects = [];

    public ApplicationImportEffectPending? GetPendingImportEffect(Uuid batchId, Uuid rowId) =>
        _pendingImportEffects.GetValueOrDefault((batchId, rowId));

    public Result RecordPendingImportEffect(ApplicationImportLedger ledger, ImportBatch batch,
        Uuid rowId, DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Stream.Realm != _tenantId.ToString())
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The import batch was not found."));
        var authority = ledger.AuthorizePendingEffect(batch, rowId, Id);
        if (!authority.IsSuccess)
            return Result.Failure(authority.Error);
        var plan = authority.Value;
        var row = plan.Rows.Single(row => row.RowId == rowId);
        var effect = new ApplicationImportEffectPending(_tenantId, Id, plan.Revision,
            plan.PlanSha256, plan.Start, row, recordedAt);
        if (GetPendingImportEffect(batch.Id, rowId) is { } existing)
            return existing with { RecordedAt = recordedAt } == effect ? Result.Success :
                Result.Failure(new RequestError(RequestErrorKind.Conflict, "The import effect has different content."));
        if (row.Decision == "create_new" && (_created || _pendingImportEffects.Count > 0))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The new import target already exists."));
        if (row.Decision == "link_existing")
        {
            if (!_created)
                return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The linked application was not found."));
            if (_retired)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The linked application is retired."));
            if (_revision != row.ExpectedApplicationRevision)
                return Result.Failure(VersionedRecordRules.StaleRevision("application", _revision).ToRequestError());
        }
        if (JsonSerializer.SerializeToUtf8Bytes(effect, ComplianceCoreJsonContext.Default.ApplicationImportEffectPending).Length >
            ImportBatch.MaximumStagedPayloadBytes)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The import effect exceeds its event payload bounds."));
        RaiseEvent(effect);
        return Result.Success;
    }

    void RegisterImportEffectEvents() => On<ApplicationImportEffectPending>(ev =>
    {
        if (ev.TenantId != _tenantId || ev.Plan.TenantId != _tenantId || ev.ApplicationId != Id ||
            ev.Row.ApplicationId != Id || ev.Plan.BatchId == Uuid.Empty || ev.Row.RowId == Uuid.Empty ||
            ev.Plan.ApproverMemberId == Uuid.Empty || ev.Plan.SubmitterMemberId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(ev.Plan.ApproverDisplay) || string.IsNullOrWhiteSpace(ev.Plan.SubmitterDisplay) ||
            ev.Plan.RowCount is < 1 or > 200 || ev.PlanRevision != ev.Plan.Revision + ev.Plan.RowCount + 1 ||
            string.IsNullOrWhiteSpace(ev.Plan.SourceKey) || string.IsNullOrWhiteSpace(ev.Plan.SourceNamespace) ||
            ev.PlanSha256.Length != 64 || ev.PlanSha256.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')) ||
            ev.Row.Decision is not ("create_new" or "link_existing") ||
            (ev.Row.Decision == "create_new" && (_created || _pendingImportEffects.Count > 0 ||
                ev.Row.ExpectedApplicationRevision is not null)) ||
            (ev.Row.Decision == "link_existing" && (!_created || _retired ||
                ev.Row.ExpectedApplicationRevision != _revision)) ||
            _pendingImportEffects.ContainsKey((ev.Plan.BatchId, ev.Row.RowId)))
            throw new InvalidOperationException("The pending import effect does not belong to its target.");
        _pendingImportEffects.Add((ev.Plan.BatchId, ev.Row.RowId), ev);
    });
}
