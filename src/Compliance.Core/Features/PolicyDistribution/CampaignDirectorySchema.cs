using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

static class CampaignDirectorySchema
{
    public static readonly KvDirectoryIndex<CampaignSummaryView> ByProgramLaunch = new(
        "by_program_launched_at", 1, static campaign =>
        [
            campaign.ProgramId.ToString(),
            campaign.LaunchedAt.UtcTicks.ToString("D20", CultureInfo.InvariantCulture),
            campaign.CampaignId.ToString(),
        ]);

    public static readonly KvDirectory<CampaignSummaryView, Uuid> Campaigns = new(
        "campaigns", ComplianceCoreJsonContext.Default.CampaignSummaryView,
        static campaign => campaign.CampaignId, static campaignId => [campaignId.ToString()],
        [ByProgramLaunch]);
}
