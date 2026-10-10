using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed partial class ApplicationImportLedger
{
    readonly Dictionary<Uuid, ApplicationImportPlanStarted> _planStarts = [];
    readonly Dictionary<Uuid, List<ApplicationImportPlannedRow>> _planRows = [];
    readonly Dictionary<Uuid, ApplicationImportFrozenPlan> _frozenPlans = [];
    Uuid? _acceptingBatchId;

    public bool HasOtherAcceptingBatch(ImportBatch batch) => _acceptingBatchId is { } id && id != batch.Id;

    public ApplicationImportFrozenPlan? GetFrozenPlan(Uuid batchId) => _frozenPlans.GetValueOrDefault(batchId);

    public Result<ApplicationImportFrozenPlan> AuthorizePendingEffect(ImportBatch batch, Uuid rowId,
        Uuid applicationId)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (!BelongsToSource(batch))
            return Result<ApplicationImportFrozenPlan>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The import batch was not found for this source."));
        var committed = GetState(batch) == "committed" && IsCommitDurable(batch.Id);
        if ((!committed && (GetState(batch) != "accepting" || _acceptingBatchId != batch.Id)) ||
            !_frozenPlans.TryGetValue(batch.Id, out var plan))
            return Result<ApplicationImportFrozenPlan>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The import batch has no active frozen plan."));
        if (!plan.Rows.Any(row => row.RowId == rowId && row.ApplicationId == applicationId))
            return Result<ApplicationImportFrozenPlan>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The effect was not found in the import plan."));
        return Result<ApplicationImportFrozenPlan>.Success(plan);
    }

    public string GetState(ImportBatch batch) => batch.IsCanceled || GetCanceledRevision(batch.Id) is not null
        ? "canceled" : _failures.ContainsKey(batch.Id) ? "failed" : _commits.ContainsKey(batch.Id) ? "committed" : _planStarts.ContainsKey(batch.Id) ? "accepting" : "preview_ready";

    public Result BeginAcceptance(ImportBatch batch, long expectedRevision,
        IReadOnlyDictionary<Uuid, DeclaredApplication> targets, Uuid approverMemberId,
        string approverDisplay, DateTimeOffset startedAt)
        => BeginAcceptance(batch, expectedRevision, targets, approverMemberId,
            approverDisplay, startedAt, null);

    public Result BeginAcceptance(ImportBatch batch, long expectedRevision,
        IReadOnlyDictionary<Uuid, DeclaredApplication> targets, Uuid approverMemberId,
        string approverDisplay, DateTimeOffset startedAt,
        IReadOnlyDictionary<Uuid, ApplicationChangePreview>? retirementImpacts)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(batch);
        if (approverMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(approverDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "An import plan requires an attributable approver."));
        if (!BelongsToSource(batch))
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The import batch was not found for this source."));
        if (batch.IsCanceled || GetCanceledRevision(batch.Id) is not null || _failures.ContainsKey(batch.Id))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The import batch is canceled or failed."));
        if (_frozenPlans.TryGetValue(batch.Id, out var existing))
            return expectedRevision == existing.Start.Revision - 1 || expectedRevision == GetRevision(batch)
                ? Result.Success : Result.Failure(VersionedRecordRules.StaleRevision("import", GetRevision(batch)).ToRequestError());
        if (_acceptingBatchId is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Another batch is accepting for this source.", isTransient: true));
        var prepared = PrepareAcceptancePlan(batch, expectedRevision, retirementImpacts);
        if (!prepared.IsSuccess)
            return Result.Failure(prepared.Error);
        foreach (var row in prepared.Value.Where(row => row.Decision is "link_existing" or "retire"))
        {
            if (!targets.TryGetValue(row.ApplicationId, out var target) || !target.IsCreated ||
                target.Id != row.ApplicationId || target.Stream.Realm != _tenantId.ToString())
                return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The linked application was not found."));
            if (row.Decision == "retire" && target.CheckPendingImportChanges() is { } unsettled)
                return Result.Failure(unsettled);
            if (target.IsRetired)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    row.Decision == "retire" ? "The retirement target is already retired." :
                        "The linked application is retired."));
            if (target.Revision != row.ExpectedApplicationRevision)
                return Result.Failure(VersionedRecordRules.StaleRevision("application", target.Revision).ToRequestError());
        }
        var revision = GetRevision(batch);
        var start = new ApplicationImportPlanStarted(_tenantId, _sourceKey, _sourceNamespace,
            batch.Id, ++revision, batch.ContentDigest!, batch.Coverage!, prepared.Value.Count,
            batch.SubmitterMemberId, batch.SubmitterDisplay!, approverMemberId, approverDisplay, startedAt);
        var rows = prepared.Value.Select(row => new ApplicationImportPlanRowFrozen(
            _tenantId, batch.Id, ++revision, row)).ToArray();
        var seal = new ApplicationImportPlanSealed(_tenantId, batch.Id, ++revision, Hash(prepared.Value));
        if (JsonSerializer.SerializeToUtf8Bytes(start, ComplianceCoreJsonContext.Default.ApplicationImportPlanStarted).Length > ImportBatch.MaximumStagedPayloadBytes ||
            rows.Any(row => JsonSerializer.SerializeToUtf8Bytes(row, ComplianceCoreJsonContext.Default.ApplicationImportPlanRowFrozen).Length > ImportBatch.MaximumStagedPayloadBytes))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The frozen import plan exceeds its event payload bounds."));
        RaiseEvent(start);
        foreach (var row in rows)
            RaiseEvent(row);
        RaiseEvent(seal);
        return Result.Success;
    }

    void RegisterAcceptanceEvents()
    {
        On<ApplicationImportPlanStarted>(ev =>
        {
            if (ev.TenantId != _tenantId || ev.SourceKey != _sourceKey || ev.SourceNamespace != _sourceNamespace ||
                _acceptingBatchId is not null || _planStarts.ContainsKey(ev.BatchId) || _canceledRevisions.ContainsKey(ev.BatchId) ||
                ev.Revision != _revisions.GetValueOrDefault(ev.BatchId, 1) + 1 ||
                ev.RowCount is < 1 or > MaximumCommitEffectRows)
                throw new InvalidOperationException("The import plan does not belong to an available source.");
            _acceptingBatchId = ev.BatchId;
            _planStarts.Add(ev.BatchId, ev);
            _planRows.Add(ev.BatchId, []);
            _revisions[ev.BatchId] = ev.Revision;
        });
        On<ApplicationImportPlanRowFrozen>(ev =>
        {
            if (ev.TenantId != _tenantId || _acceptingBatchId != ev.BatchId ||
                !_planRows.TryGetValue(ev.BatchId, out var rows) || _frozenPlans.ContainsKey(ev.BatchId) ||
                rows.Count >= _planStarts[ev.BatchId].RowCount || ev.Revision != _revisions[ev.BatchId] + 1 ||
                rows.Any(row => row.RowId == ev.Row.RowId || row.SourceRecordId == ev.Row.SourceRecordId) ||
                (ev.Row.Decision == "retire" && !MatchesRetirementProposal(ev.BatchId,
                    _planStarts[ev.BatchId], ev.Row)) ||
                (ev.Row.Decision != "retire" && (ev.Row.RetirementSourceClaimId is not null ||
                    ev.Row.RetirementProposalSha256 is not null || ev.Row.RetirementImpactDigest is not null ||
                    ev.Row.RetirementReason is not null)) ||
                ConflictsWithRecordedClaim(ev.Row))
                throw new InvalidOperationException("The frozen import row does not belong to its plan.");
            rows.Add(ev.Row);
            _revisions[ev.BatchId] = ev.Revision;
        });
        On<ApplicationImportPlanSealed>(ev =>
        {
            if (ev.TenantId != _tenantId || _acceptingBatchId != ev.BatchId ||
                !_planRows.TryGetValue(ev.BatchId, out var rows) || _frozenPlans.ContainsKey(ev.BatchId) ||
                rows.Count != _planStarts[ev.BatchId].RowCount || ev.Revision != _revisions[ev.BatchId] + 1 || Hash(rows) != ev.PlanSha256)
                throw new InvalidOperationException("The frozen import plan is incomplete or altered.");
            _frozenPlans.Add(ev.BatchId, new ApplicationImportFrozenPlan(_planStarts[ev.BatchId],
                Array.AsReadOnly(rows.ToArray()), ev.Revision, ev.PlanSha256));
            _planSealVersions.Add(ev.BatchId, ev.Metadata.AggregateVersion);
            _revisions[ev.BatchId] = ev.Revision;
        });
    }

    static string Hash(IReadOnlyList<ApplicationImportPlannedRow> rows) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(rows,
            ComplianceCoreJsonContext.Default.IReadOnlyListApplicationImportPlannedRow)));

    bool MatchesRetirementProposal(Uuid batchId, ApplicationImportPlanStarted plan,
        ApplicationImportPlannedRow row)
    {
        if (!ApplicationImportRetirementImpact.IsValidFrozenRow(plan, row) ||
            !_retirementProposals.TryGetValue(batchId, out var proposal) ||
            proposal.ProposalSha256 != row.RetirementProposalSha256 ||
            proposal.Start.Reason != row.RetirementReason)
            return false;
        return proposal.Rows.Any(retirement => retirement.SourceClaimId == row.RetirementSourceClaimId &&
            retirement.SourceRecordId == row.SourceRecordId && retirement.ApplicationId == row.ApplicationId &&
            retirement.ExpectedApplicationRevision == row.ExpectedApplicationRevision);
    }
}
