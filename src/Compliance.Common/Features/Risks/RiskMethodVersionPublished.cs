using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.method.version_published", 1)]
public sealed record RiskMethodVersionPublished(Uuid TenantId, Uuid ProgramId,
    RiskMethodVersionView Version) : DomainEvent;
