using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application_import.retirement_proposal_sealed", 1)]
public sealed record ApplicationImportRetirementProposalSealed(Uuid TenantId, Uuid BatchId,
    long Revision, string ProposalSha256) : DomainEvent;
