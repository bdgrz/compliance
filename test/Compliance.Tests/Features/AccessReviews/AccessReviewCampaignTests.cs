using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class AccessReviewCampaignTests
{
    [Fact]
    public async Task ShouldFreezePopulationReviewersInstructionsAndDeadlineGivenLaunch()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);

        // Act
        var launched = await fixture.LaunchAsync(populationId);
        await fixture.ClassifyAsync(populationId, "ada", AccessReviewVocabulary.Unclassified, count: 1);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var snapshot = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new PopulationSnapshot(fixture.TenantId, launched.SnapshotId));

        // Assert
        Assert.Equal(5, launched.ItemCount);
        Assert.Equal(launched.ItemCount, campaign.UnresolvedCount);
        Assert.All(campaign.Items, item => Assert.Equal("unresolved", item.Status));
        Assert.Equal("human", Assert.Single(campaign.Items, item =>
            item.Item.ProviderSubjectId == "ada" && item.Item.ProviderEntitlementId == "deploy").Item.Classification);
        var reviewer = Assert.Single(campaign.Reviewers);
        Assert.False(reviewer.Delegated);
        Assert.True(snapshot.HasIntactContent);
        Assert.Equal(AccessReviewCampaignContent.LaunchKind, snapshot.Kind);
        Assert.Equal(launched.ContentSha256, campaign.ContentSha256);
        Assert.Contains(snapshot.Rows, row => row.Key == "header");
    }

    [Fact]
    public async Task ShouldRequireDelegationReasonGivenReviewerOtherThanAccessOwner()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        var launch = new LaunchAccessReviewCampaign(fixture.TenantId, "Review", "Instructions",
            DateTimeOffset.UtcNow.AddDays(7), [new AccessReviewAssignment(populationId, fixture.ManagerMemberId)]);

        // Act
        var refused = await fixture.FailAsync(fixture.ManagerUserId, launch, RequestErrorKind.Validation);
        var delegated = await fixture.LaunchAsync(populationId, fixture.ManagerMemberId,
            "Access owner on leave.");
        var campaign = await fixture.CampaignAsync(delegated.CampaignId);

        // Assert
        Assert.Contains("delegat", refused.Message, StringComparison.Ordinal);
        Assert.True(Assert.Single(campaign.Reviewers).Delegated);
    }

    [Fact]
    public async Task ShouldRecordAttributableDecisionsAndLimitParticipantsGivenAssignedReviewer()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var adaDeploy = ItemId(campaign, "ada", "deploy");
        var raeAdmin = ItemId(campaign, "rae", "admin");

        // Act
        var decision = await fixture.SendAsync(fixture.ReviewerUserId, new RecordAccessDecision(
            fixture.TenantId, launched.CampaignId, adaDeploy, 1, "revoke", "No longer deploys."));
        var selfReview = await fixture.FailAsync(fixture.ReviewerUserId, new RecordAccessDecision(
            fixture.TenantId, launched.CampaignId, raeAdmin, 2, "keep", "Mine."), RequestErrorKind.Forbidden);
        var outsiderDecision = await fixture.FailAsync(fixture.OutsiderUserId, new RecordAccessDecision(
            fixture.TenantId, launched.CampaignId, adaDeploy, 2, "keep", "Keep."), RequestErrorKind.NotFound);
        var outsiderRead = await fixture.FailAsync(fixture.OutsiderUserId,
            new GetAccessReviewCampaign(fixture.TenantId, launched.CampaignId), RequestErrorKind.NotFound);
        var reviewerView = await fixture.CampaignAsync(launched.CampaignId, fixture.ReviewerUserId);

        // Assert
        Assert.Equal(fixture.ReviewerMemberId, decision.ReviewerMemberId);
        Assert.Equal("revoke", decision.Decision);
        Assert.False(decision.AfterDeadline);
        Assert.Contains("own access", selfReview.Message, StringComparison.Ordinal);
        Assert.NotNull(outsiderDecision);
        Assert.NotNull(outsiderRead);
        Assert.Equal("decided", Assert.Single(reviewerView.Items, item => item.Item.ItemId == adaDeploy).Status);
        Assert.Equal(campaign.Items.Count - 1, reviewerView.UnresolvedCount);
    }

    [Fact]
    public async Task ShouldRequirePreviewTokenAndExcludePrivilegedGivenBulkDecision()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        await fixture.ApprovedExpectationAsync(0, AccessReviewVocabulary.PrivilegedEntitlement,
            new AccessExpectationParameters(EntitlementKind: "admin_privilege"));
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var adaAdmin = ItemId(campaign, "ada", "admin");
        var botDeploy = ItemId(campaign, "bot", "deploy");
        var legacyRead = ItemId(campaign, "legacy", "read");

        // Act
        var preview = await fixture.SendAsync(fixture.ReviewerUserId, new PreviewBulkAccessDecision(
            fixture.TenantId, launched.CampaignId, [adaAdmin, botDeploy, legacyRead], "keep"));
        var forged = await fixture.FailAsync(fixture.ReviewerUserId, new RecordBulkAccessDecision(
            fixture.TenantId, launched.CampaignId, [botDeploy, legacyRead], "keep", "Still needed.",
            new string('0', 64)), RequestErrorKind.Conflict);
        var recorded = await fixture.SendAsync(fixture.ReviewerUserId, new RecordBulkAccessDecision(
            fixture.TenantId, launched.CampaignId, [botDeploy, legacyRead], "keep", "Still needed.",
            preview.PreviewToken));
        var stale = await fixture.FailAsync(fixture.ReviewerUserId, new RecordBulkAccessDecision(
            fixture.TenantId, launched.CampaignId, [botDeploy, legacyRead], "keep", "Again.",
            preview.PreviewToken), RequestErrorKind.Conflict);

        // Assert
        Assert.Equal("privileged_requires_individual_decision",
            Assert.Single(preview.Rejected).Reason);
        Assert.Equal(2, preview.Eligible.Count);
        Assert.NotNull(forged);
        Assert.Equal(2, recorded.Decisions.Count);
        Assert.All(recorded.Decisions, item => Assert.Equal(preview.PreviewToken, item.BulkPreviewToken));
        Assert.Single(recorded.Decisions.Select(item => item.Rationale).Distinct());
        Assert.NotNull(stale);
    }

    internal static Uuid ItemId(AccessReviewCampaignView campaign, string subject, string entitlement) =>
        Assert.Single(campaign.Items, item => item.Item.ProviderSubjectId == subject &&
                                              item.Item.ProviderEntitlementId == entitlement).Item.ItemId;
}
