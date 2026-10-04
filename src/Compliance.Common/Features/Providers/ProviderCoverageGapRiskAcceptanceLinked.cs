using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

[Discriminator("bdgrz.provider.coverage_gap.risk_acceptance_linked", 1)]
public sealed record ProviderCoverageGapRiskAcceptanceLinked(Uuid TenantId, Uuid GapId,
    Uuid ProviderId, Uuid RequestId, long Revision,
    ProviderCoverageGapRiskAcceptanceView Acceptance) : DomainEvent;
