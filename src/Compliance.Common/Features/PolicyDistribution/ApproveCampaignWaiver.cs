using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     The Compliance Lead's personal approval of a bounded exception of at most 12 months.
///     HTTP-only. An excepted person never counts as acknowledged.
/// </summary>
[Discriminator("bdgrz.campaign.waiver.approve", 1)]
public sealed record ApproveCampaignWaiver(Uuid TenantId, Uuid ProgramId, Uuid CampaignId,
    Uuid PersonId, string Reason, DateOnly ExpiresOn)
    : IRequest<CampaignWaiverView>, IProgramScopedRequest, ICallable;
