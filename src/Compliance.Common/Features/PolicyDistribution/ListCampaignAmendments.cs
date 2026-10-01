using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

[Discriminator("bdgrz.campaign.amendments.list", 1)]
public sealed record ListCampaignAmendments(Uuid TenantId, Uuid ProgramId, Uuid CampaignId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<CampaignAmendmentView>>, IProgramScopedRequest, ICallable;
