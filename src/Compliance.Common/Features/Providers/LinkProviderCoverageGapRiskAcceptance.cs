using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Links an existing R1-07 acceptance without resolving or closing the provider gap.</summary>
[Discriminator("bdgrz.provider.coverage_gap.link_risk_acceptance", 1)]
public sealed record LinkProviderCoverageGapRiskAcceptance(Uuid TenantId, Uuid ProgramId,
    Uuid ProviderId, Uuid GapId, long ExpectedRevision, Uuid RiskId, Uuid AcceptanceId)
    : IRequest<ProviderCoverageGapRegistration>, IProviderManagementRequest,
        IProgramScopedRequest, ICallable;
