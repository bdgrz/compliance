using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application_import.retirement_proposal_started", 1)]
public sealed record ApplicationImportRetirementProposalStarted(Uuid TenantId, string SourceKey,
    string SourceNamespace, Uuid BatchId, long Revision, ulong SourcePosition, string ContentSha256,
    IReadOnlyList<string> PresentSourceRecordIds, int RowCount, Uuid MemberId, string MemberDisplay,
    string Reason, DateTimeOffset PreparedAt) : DomainEvent;
