using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class AccessRemediationTests
{
    [Fact]
    public async Task ShouldBlockCompletionUntilLaterPopulationVerifiesGivenRevokeDecision()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId, fixture.ManagerMemberId, "Owner away.",
            fixture.ApproverMemberId);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var raeAdmin = AccessReviewCampaignTests.ItemId(campaign, "rae", "admin");
        var revision = 1L;
        foreach (var item in campaign.Items.Where(item => item.Item.ItemId != raeAdmin))
            await fixture.SendAsync(fixture.ManagerUserId, new RecordAccessDecision(fixture.TenantId,
                launched.CampaignId, item.Item.ItemId, revision++, "keep", "Still needed."));

        // Act
        var blocked = await fixture.FailAsync(fixture.ManagerUserId, new CompleteAccessReviewCampaign(
            fixture.TenantId, launched.CampaignId, revision, "Complete."), RequestErrorKind.Conflict);
        await fixture.SendAsync(fixture.ManagerUserId, new RecordAccessDecision(fixture.TenantId,
            launched.CampaignId, raeAdmin, revision++, "revoke", "Rae changed teams."));
        await fixture.SendAsync(fixture.ApproverUserId, new RecordAccessRemediationChange(
            fixture.TenantId, launched.CampaignId, raeAdmin, revision++, "JIRA-42",
            "Removed AdministratorAccess in the console.", DateTimeOffset.UtcNow));
        var stillBlocked = await fixture.FailAsync(fixture.ManagerUserId, new CompleteAccessReviewCampaign(
            fixture.TenantId, launched.CampaignId, revision, "Complete."), RequestErrorKind.Conflict);
        var (unchangedId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts(),
            DateTimeOffset.UtcNow);
        var unchanged = await fixture.FailAsync(fixture.ManagerUserId, new VerifyAccessRemediation(
            fixture.TenantId, launched.CampaignId, raeAdmin, revision, unchangedId), RequestErrorKind.Conflict);
        var (laterId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts(raeHasAdmin: false),
            DateTimeOffset.UtcNow);
        var verified = await fixture.SendAsync(fixture.ManagerUserId, new VerifyAccessRemediation(
            fixture.TenantId, launched.CampaignId, raeAdmin, revision++, laterId));
        var completion = await fixture.SendAsync(fixture.ManagerUserId, new CompleteAccessReviewCampaign(
            fixture.TenantId, launched.CampaignId, revision, "All decisions recorded and verified."));
        var final = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new PopulationSnapshot(fixture.TenantId, completion.SnapshotId));
        var completed = await fixture.CampaignAsync(launched.CampaignId);

        // Assert
        Assert.Contains("unresolved", blocked.Message, StringComparison.Ordinal);
        Assert.Contains("remediation", stillBlocked.Message, StringComparison.Ordinal);
        Assert.Contains("unchanged", unchanged.Message, StringComparison.Ordinal);
        Assert.Equal("removed", verified.Outcome);
        Assert.Equal(laterId, verified.PopulationId);
        Assert.Equal("completed", completed.Status);
        var rae = Assert.Single(completed.Items, item => item.Item.ItemId == raeAdmin);
        Assert.Equal("verified", rae.RemediationStatus);
        Assert.Single(rae.ProviderChanges);
        Assert.True(final.HasIntactContent);
        Assert.Equal(AccessReviewCampaignContent.ResultKind, final.Kind);
        Assert.Equal(completed.Items.Count + 1, final.RowCount);
        await fixture.FailAsync(fixture.ManagerUserId, new RecordAccessDecision(fixture.TenantId,
            launched.CampaignId, raeAdmin, completed.Revision, "keep", "Late."), RequestErrorKind.Conflict);
    }

    [Fact]
    public async Task ShouldCompleteWithApprovedExceptionGivenUnverifiedModifyDecision()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId, fixture.ManagerMemberId, "Owner away.",
            fixture.ApproverMemberId);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var revision = 1L;
        var botDeploy = AccessReviewCampaignTests.ItemId(campaign, "bot", "deploy");
        foreach (var item in campaign.Items)
            await fixture.SendAsync(fixture.ManagerUserId, new RecordAccessDecision(fixture.TenantId,
                launched.CampaignId, item.Item.ItemId, revision++,
                item.Item.ItemId == botDeploy ? "modify" : "keep", "Reviewed."));

        // Act
        var reviewerException = await fixture.FailAsync(fixture.ManagerUserId,
            new ExemptAccessRemediation(fixture.TenantId, launched.CampaignId, botDeploy, revision,
                "Vendor fix pending."), RequestErrorKind.Forbidden);
        var exception = await fixture.SendAsync(fixture.ApproverUserId, new ExemptAccessRemediation(
            fixture.TenantId, launched.CampaignId, botDeploy, revision++, "Vendor fix pending.",
            DateTimeOffset.UtcNow.AddDays(30)));
        var completion = await fixture.SendAsync(fixture.ManagerUserId, new CompleteAccessReviewCampaign(
            fixture.TenantId, launched.CampaignId, revision, "Completed with one approved exception."));
        var completed = await fixture.CampaignAsync(launched.CampaignId);

        // Assert
        Assert.NotNull(reviewerException);
        Assert.Equal("excepted", Assert.Single(completed.Items, item => item.Item.ItemId == botDeploy)
            .RemediationStatus);
        Assert.Equal(exception.ExceptionId, completed.Items.Single(item => item.Item.ItemId == botDeploy)
            .Exception!.ExceptionId);
        Assert.Equal(completion.SnapshotId, completed.Completion!.SnapshotId);
    }
}
