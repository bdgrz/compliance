using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

/// <summary>
/// Reports current Control-draft applicability for changed governed boundary scope while
/// deliberately withholding lifecycle-complete approval clearance.
/// </summary>
public sealed class ControlBoundaryImpactContributor(
    IApplicationControlDraftReferenceDirectory references,
    ApplicationControlDraftReferenceReadConsistency consistency) : IBoundaryImpactContributor
{
    const int MaximumRecords = 200;

    public string Context => "controls";

    public async ValueTask<Result<BoundaryImpactContribution>> ContributeAsync(BoundaryView boundary,
        IReadOnlyList<BoundaryChange> changes, CancellationToken ct)
    {
        if (boundary.LatestApprovedVersion is null)
            return Result<BoundaryImpactContribution>.Success(
                new BoundaryImpactContribution(Context, [], true));

        var targets = ChangedScopeTargets(changes);
        if (targets.Count == 0)
            return Result<BoundaryImpactContribution>.Success(
                new BoundaryImpactContribution(Context, [], false));

        var beforeRead = await consistency.CaptureAsync(boundary.TenantId, ct)
            .ConfigureAwait(false);
        if (!beforeRead.IsSuccess)
            return Result<BoundaryImpactContribution>.Failure(beforeRead.Error);
        var fence = beforeRead.Value;

        var records = new List<BoundaryAffectedRecord>();
        var controlIds = new HashSet<Uuid>();
        var scanBudget = MaximumRecords;
        foreach (var target in targets)
        {
            if (scanBudget == 0)
                break;
            var page = await references.ListAsync(boundary.TenantId, target.SubjectType,
                target.RecordId, scanBudget, null, ct).ConfigureAwait(false);
            if (page.Items.Count > scanBudget || page.Items.Any(item =>
                    item.TenantId != boundary.TenantId ||
                    item.SubjectType != target.SubjectType ||
                    item.GovernedRecordId != target.RecordId ||
                    item.ControlId == Uuid.Empty || item.ProgramId == Uuid.Empty))
                return await ConfirmResultAsync(Result<BoundaryImpactContribution>.Failure(
                    new RequestError(RequestErrorKind.Conflict,
                        "The control draft reference projection is inconsistent.", isTransient: true)),
                    fence, boundary.TenantId, ct).ConfigureAwait(false);

            scanBudget -= page.Items.Count;
            foreach (var reference in page.Items.Where(item => item.ProgramId == boundary.ProgramId)
                         .OrderBy(static item => item.ControlId.ToString(), StringComparer.Ordinal)
                         .ThenBy(static item => item.EntryId.ToString(), StringComparer.Ordinal))
            {
                if (!controlIds.Add(reference.ControlId))
                    continue;
                records.Add(new BoundaryAffectedRecord(boundary.TenantId, Context,
                    "control_draft", reference.ControlId,
                    "The current Control draft applies to changed governed boundary scope."));
            }
        }

        return await ConfirmResultAsync(Result<BoundaryImpactContribution>.Success(
            new BoundaryImpactContribution(Context, [.. records.OrderBy(static item =>
                item.RecordId.ToString(), StringComparer.Ordinal)], false)), fence, boundary.TenantId, ct)
            .ConfigureAwait(false);
    }

    async ValueTask<Result<BoundaryImpactContribution>> ConfirmResultAsync(
        Result<BoundaryImpactContribution> candidate, ProjectionCheckpoint fence, Uuid tenantId,
        CancellationToken ct)
    {
        var confirmation = await consistency.ConfirmUnchangedAndCaughtUpAsync(tenantId, fence, ct)
            .ConfigureAwait(false);
        return confirmation.IsSuccess
            ? candidate
            : Result<BoundaryImpactContribution>.Failure(confirmation.Error);
    }

    static List<ScopeTarget> ChangedScopeTargets(IReadOnlyList<BoundaryChange> changes) =>
        [.. changes.Where(static change => change.Field == "scope_entry")
            .SelectMany(static change => new[] { change.PreviousEntry, change.ProposedEntry })
            .Where(static entry => entry is { Unresolved: false, GovernedRecordId: { } id } &&
                                  id != Uuid.Empty &&
                                  entry.SubjectType is "application" or "system_instance")
            .Select(static entry => new ScopeTarget(entry!.SubjectType,
                entry.GovernedRecordId!.Value))
            .Distinct()
            .OrderBy(static target => target.SubjectType, StringComparer.Ordinal)
            .ThenBy(static target => target.RecordId.ToString(), StringComparer.Ordinal)];

    sealed record ScopeTarget(string SubjectType, Uuid RecordId);
}
