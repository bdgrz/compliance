using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>Closes a campaign and freezes its reconciled result.</summary>
[Discriminator("bdgrz.campaign.close", 1)]
public sealed record CloseCampaign(Uuid TenantId, Uuid ProgramId, Uuid CampaignId,
    string Rationale)
    : IRequest<CampaignView>, IProgramScopedRequest, ICallable;
