using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

static class AccessReviewDirectorySchema
{
    public const string PopulationProjector = "AccessPopulationDirectoryV1";
    public const string CampaignProjector = "AccessReviewCampaignDirectoryV1";

    public static readonly KvDirectoryIndex<AccessPopulationSummaryView> PopulationsByInstance = new(
        "by_instance_observed_at", 1, static population =>
            [population.SystemInstanceId.ToString(),
                population.ObservedAt.ToUniversalTime().Ticks.ToString("D20", CultureInfo.InvariantCulture),
                population.PopulationId.ToString()]);

    public static readonly KvDirectory<AccessPopulationSummaryView, Uuid> Populations = new(
        "access_populations", ComplianceCoreJsonContext.Default.AccessPopulationSummaryView,
        static population => population.PopulationId, static id => [id.ToString()],
        [PopulationsByInstance]);

    public static readonly KvDirectoryIndex<AccessReviewCampaignSummaryView> CampaignsByLaunch = new(
        "by_launched_at", 1, static campaign =>
            [campaign.LaunchedAt.ToUniversalTime().Ticks.ToString("D20", CultureInfo.InvariantCulture),
                campaign.CampaignId.ToString()]);

    public static readonly KvDirectory<AccessReviewCampaignSummaryView, Uuid> Campaigns = new(
        "access_review_campaigns", ComplianceCoreJsonContext.Default.AccessReviewCampaignSummaryView,
        static campaign => campaign.CampaignId, static id => [id.ToString()], [CampaignsByLaunch]);

    public static EventStreamPattern PopulationPattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), AccessPopulation.Area);

    public static EventStreamPattern CampaignPattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), AccessReviewCampaign.Area);
}
