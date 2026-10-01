using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

public static class CampaignSource
{
    public static async ValueTask<Result<PolicyDistributionCampaign>> ReadAsync(
        IAggregateReader reader, Uuid tenantId, Uuid programId, Uuid campaignId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var campaign = await reader.HydrateAsync(new PolicyDistributionCampaign(tenantId,
            campaignId), ct).ConfigureAwait(false);
        return campaign.IsLaunched && campaign.ProgramId == programId
            ? Result<PolicyDistributionCampaign>.Success(campaign)
            : Result<PolicyDistributionCampaign>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The campaign was not found."));
    }
}
