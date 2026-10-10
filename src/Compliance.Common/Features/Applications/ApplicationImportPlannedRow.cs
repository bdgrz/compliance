using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed record ApplicationImportPlannedRow(Uuid RowId, string SourceRecordId,
    string Decision, Uuid ApplicationId, long? ExpectedApplicationRevision,
    string Name, string Purpose, string? OwnerReference)
{
    public Uuid? RetirementSourceClaimId { get; init; }
    public string? RetirementProposalSha256 { get; init; }
    public string? RetirementImpactDigest { get; init; }
    public string? RetirementReason { get; init; }
}
