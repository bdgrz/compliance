using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

[Discriminator("bdgrz.campaigns.list", 1)]
public sealed record ListCampaigns(Uuid TenantId, Uuid ProgramId, int? Limit = null,
    string? Cursor = null)
    : IRequest<Page<CampaignSummaryView>>, IProgramReadRequest, ICallable;
