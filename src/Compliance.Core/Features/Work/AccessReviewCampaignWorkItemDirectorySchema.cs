using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

static class AccessReviewCampaignWorkItemDirectorySchema
{
    public static readonly KvDirectory<AccountableWorkItemProjectionRevision, string> Revisions =
        new("projection_revisions",
            ComplianceCoreJsonContext.Default.AccountableWorkItemProjectionRevision,
            static revision => revision.ProjectorName,
            static projectorName => [projectorName], []);

    public static readonly KvDirectoryIndex<AccessReviewCampaignWorkCampaignState> CampaignsByProgram =
        new("by_program", 1, static campaign =>
            [campaign.ProgramId.ToString(), campaign.CampaignId.ToString()]);

    public static readonly KvDirectory<AccessReviewCampaignWorkCampaignState, Uuid> Campaigns = new(
        "campaigns", ComplianceCoreJsonContext.Default.AccessReviewCampaignWorkCampaignState,
        static campaign => campaign.CampaignId,
        static campaignId => [campaignId.ToString()], [CampaignsByProgram]);

    public static readonly KvDirectoryIndex<AccessReviewCampaignWorkItemState> ItemsByProgram =
        new("by_program", 1, static item =>
            [item.ProgramId.ToString(), item.CampaignId.ToString(), item.ItemId.ToString()]);

    public static readonly KvDirectory<AccessReviewCampaignWorkItemState, string> Items = new(
        "items", ComplianceCoreJsonContext.Default.AccessReviewCampaignWorkItemState,
        static item => ItemKey(item.CampaignId, item.ItemId),
        static key => [key], [ItemsByProgram]);

    public static string ItemKey(Uuid campaignId, Uuid itemId) => $"{campaignId}:{itemId}";
}
