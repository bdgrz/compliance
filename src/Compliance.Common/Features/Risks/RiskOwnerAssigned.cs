using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.owner.assigned", 1)]
public sealed record RiskOwnerAssigned(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision, RiskOwnerView Owner) : DomainEvent;
