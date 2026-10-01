using Bdgrz.Compliance.Features.Policies;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>Reads aggregate campaign evidence; person-level detail needs program management.</summary>
public sealed class GetCampaignHandler(IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<GetCampaign, CampaignView>
{
    public async ValueTask<Result<CampaignView>> HandleAsync(IRequestContext<GetCampaign> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var campaign = await CampaignSource.ReadAsync(reader, request.TenantId,
            request.ProgramId, request.CampaignId, ct).ConfigureAwait(false);
        return campaign.IsSuccess
            ? Result<CampaignView>.Success(campaign.Value.ToView(request.AsOf ??
                PolicySource.Today(clock)))
            : Result<CampaignView>.Failure(campaign.Error);
    }
}
