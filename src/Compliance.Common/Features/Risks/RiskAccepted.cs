using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.accepted", 1)]
public sealed record RiskAccepted(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision, RiskAcceptanceView Acceptance) : DomainEvent;
