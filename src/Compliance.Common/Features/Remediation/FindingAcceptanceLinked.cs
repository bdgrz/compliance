using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

[Discriminator("bdgrz.finding.acceptance.linked", 1)]
public sealed record FindingAcceptanceLinked(Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long Revision, FindingAcceptanceView Acceptance) : DomainEvent;
