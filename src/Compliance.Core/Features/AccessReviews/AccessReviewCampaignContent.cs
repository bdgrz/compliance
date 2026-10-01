using Bdgrz.Compliance.Features.Snapshots;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Canonical v1 rows of the launch snapshot (instructions, deadline, reviewers, and frozen
///     items) and of the final snapshot (every item with its decisions, remediation, and evidence).
/// </summary>
public static class AccessReviewCampaignContent
{
    public const string LaunchKind = "access_review_campaign";
    public const string ResultKind = "access_review_campaign_result";

    public static IReadOnlyList<PopulationRow> LaunchRows(AccessReviewCampaignHeader header,
        IReadOnlyList<AccessReviewerView> reviewers, IReadOnlyList<AccessReviewItemView> items)
    {
        var json = ComplianceCoreJsonContext.Default;
        var rows = new List<PopulationRow> { SnapshotRows.Row("header", header, json.AccessReviewCampaignHeader) };
        rows.AddRange(reviewers.Select(reviewer => SnapshotRows.Row(
            "reviewer/" + reviewer.PopulationId, reviewer, json.AccessReviewerView)));
        rows.AddRange(items.Select(item => SnapshotRows.Row("item/" + item.ItemId, item,
            json.AccessReviewItemView)));
        SnapshotRows.Sort(rows);
        return rows;
    }

    public static IReadOnlyList<PopulationRow> ResultRows(AccessReviewCampaignResultHeader header,
        IReadOnlyList<AccessReviewItemStateView> items)
    {
        var json = ComplianceCoreJsonContext.Default;
        var rows = new List<PopulationRow>
        {
            SnapshotRows.Row("header", header, json.AccessReviewCampaignResultHeader),
        };
        rows.AddRange(items.Select(item => SnapshotRows.Row("item/" + item.Item.ItemId, item,
            json.AccessReviewItemStateView)));
        SnapshotRows.Sort(rows);
        return rows;
    }
}
