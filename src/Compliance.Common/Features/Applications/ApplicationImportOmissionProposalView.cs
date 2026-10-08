using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed record ApplicationImportOmissionProposalView(Uuid TenantId, Uuid BatchId, string SourceKey,
    string SourceNamespace, long Revision, ulong SourcePosition, string ContentSha256,
    IReadOnlyList<string> PresentSourceRecordIds, IReadOnlyList<ApplicationImportRetirementRow> MissingRows,
    Uuid PreparerMemberId, string PreparerDisplay, string Reason, DateTimeOffset PreparedAt,
    string ProposalSha256, IReadOnlyList<string> AcceptanceBlockers);
