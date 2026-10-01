using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

public sealed class ListCampaignAmendmentsHandler(IAggregateReader reader)
    : IRequestHandler<ListCampaignAmendments, Page<CampaignAmendmentView>>
{
    public async ValueTask<Result<Page<CampaignAmendmentView>>> HandleAsync(
        IRequestContext<ListCampaignAmendments> context, CancellationToken ct)
    {
        var request = context.Request;
        var campaign = await CampaignSource.ReadAsync(reader, request.TenantId,
            request.ProgramId, request.CampaignId, ct).ConfigureAwait(false);
        return campaign.IsSuccess
            ? ControlActivationSource.Paginate(campaign.Value.Amendments(), request.Limit,
                request.Cursor, "campaign amendments")
            : Result<Page<CampaignAmendmentView>>.Failure(campaign.Error);
    }
}
