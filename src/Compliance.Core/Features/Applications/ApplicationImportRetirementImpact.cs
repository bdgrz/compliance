using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

static class ApplicationImportRetirementImpact
{
    public static bool IsUsable(ApplicationChangePreview impact, Uuid tenantId,
        ApplicationImportRetirementRow retirement)
    {
        ArgumentNullException.ThrowIfNull(impact);
        ArgumentNullException.ThrowIfNull(retirement);
        return impact.TenantId == tenantId && impact.ApplicationId == retirement.ApplicationId &&
               impact.ApplicationRevision == retirement.ExpectedApplicationRevision &&
               IsCompleteWithoutKnownReferences(impact) && IsDigest(impact.ImpactDigest);
    }

    public static bool MatchesFrozen(ApplicationChangePreview impact, Uuid tenantId,
        ApplicationImportPlannedRow row) => row.ExpectedApplicationRevision is { } revision &&
        impact.TenantId == tenantId && impact.ApplicationId == row.ApplicationId &&
        impact.ApplicationRevision == revision && IsCompleteWithoutKnownReferences(impact) &&
        IsDigest(impact.ImpactDigest) &&
        string.Equals(impact.ImpactDigest, row.RetirementImpactDigest, StringComparison.Ordinal);

    public static bool IsValidFrozenRow(ApplicationImportPlanStarted plan,
        ApplicationImportPlannedRow row) => row.Decision == "retire" &&
        row.ExpectedApplicationRevision is >= 1 && row.RetirementSourceClaimId is { } claimId &&
        claimId != Uuid.Empty && row.RetirementProposalSha256 is { } proposalSha && IsDigest(proposalSha) &&
        row.RetirementImpactDigest is { } impactSha && IsDigest(impactSha) &&
        row.RetirementReason is { Length: > 0 and <= 2000 } reason && reason == reason.Trim() &&
        !string.IsNullOrWhiteSpace(row.SourceRecordId) && row.SourceRecordId.Length <= 256 &&
        row.SourceRecordId == row.SourceRecordId.Trim() && row.Name is { Length: 0 } &&
        row.Purpose is { Length: 0 } &&
        row.OwnerReference is null && row.RowId == Uuid.CreateVersion5(plan.BatchId,
            $"application_import_retirement:{claimId}") && claimId == Uuid.CreateVersion5(
            ApplicationImportLedger.IdFor(plan.TenantId, plan.SourceKey, plan.SourceNamespace),
            $"application_import_claim:{row.SourceRecordId}");

    static bool IsCompleteWithoutKnownReferences(ApplicationChangePreview impact) =>
        impact.ChangeKind == "retire" && impact.Complete && impact.PendingContexts is { Count: 0 } &&
        impact.Changes is { Count: 0 } && impact.BoundaryReferences is { Count: 0 } &&
        impact.ControlDraftReferences is { Count: 0 } && impact.SystemInstanceReferences is { Count: 0 };

    static bool IsDigest(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
