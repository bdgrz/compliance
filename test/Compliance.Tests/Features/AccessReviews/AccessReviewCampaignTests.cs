using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class AccessReviewCampaignTests
{
    [Fact]
    public async Task ShouldHideRestrictedPopulationGivenCampaignLaunchWithoutReadGrant()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync(
            applicationRestricted: true);
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());

        // Act
        var denied = await fixture.FailAsync(fixture.ManagerUserId,
            new LaunchAccessReviewCampaign(fixture.TenantId, "Q3 review", "Review access.",
                DateTimeOffset.UtcNow.AddDays(14),
                [new AccessReviewAssignment(populationId, fixture.ReviewerMemberId)],
                fixture.ProgramId, fixture.RemediationOwnerMemberId),
            RequestErrorKind.NotFound);

        // Assert
        Assert.NotNull(denied);
    }

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
        Assert.Contains(fixture.ProgramId.ToString(),
            snapshot.Rows.Single(row => row.Key == "header").Content.GetRawText(),
            StringComparison.Ordinal);
        Assert.Contains(fixture.RemediationOwnerMemberId.ToString(),
            snapshot.Rows.Single(row => row.Key == "header").Content.GetRawText(),
            StringComparison.Ordinal);
        var aggregate = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new AccessReviewCampaign(fixture.TenantId, launched.CampaignId));
        Assert.Equal(fixture.ProgramId, aggregate.Launched!.ProgramId);
        Assert.Equal(fixture.RemediationOwnerMemberId,
            aggregate.Launched.RemediationOwnerMemberId);
    }

    [Fact]
    public async Task ShouldRequireCurrentProgramManagementGivenIdempotentLaunchReplay()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var request = new LaunchAccessReviewCampaign(fixture.TenantId, "Q3 AWS review",
            "Keep only access each person still needs.", DateTimeOffset.UtcNow.AddDays(14),
            [new AccessReviewAssignment(populationId, fixture.ReviewerMemberId)],
            fixture.ProgramId, fixture.RemediationOwnerMemberId);
        var requestId = Uuid.CreateVersion4();
        var metadata = new RequestMetadata(requestId, requestId, null);
        await fixture.As(fixture.ManagerUserId).GivenMetadata(metadata)
            .When(request).ExpectSuccess();
        fixture.Permissions.Deny(fixture.ManagerUserId, RbacPermissions.ProgramManage);

        // Act
        var refused = await fixture.As(fixture.ManagerUserId).GivenMetadata(metadata)
            .When(request).ExpectFailure(RequestErrorKind.Forbidden);

        // Assert
        Assert.Contains("currently manage", refused.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldHideRestrictedCampaignGivenIdempotentLaunchReplayWithoutSystemReadGrant()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync(applicationRestricted: true);
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        fixture.AllowManagerRestrictedRead();
        fixture.Permissions.AllowRestrictedRead(fixture.ReviewerUserId);
        await fixture.ClassifyStandardAsync(populationId);
        var request = new LaunchAccessReviewCampaign(fixture.TenantId, "Q3 AWS review",
            "Keep only access each person still needs.", DateTimeOffset.UtcNow.AddDays(14),
            [new AccessReviewAssignment(populationId, fixture.ReviewerMemberId)],
            fixture.ProgramId, fixture.RemediationOwnerMemberId);
        var requestId = Uuid.CreateVersion4();
        var metadata = new RequestMetadata(requestId, requestId, null);
        await fixture.As(fixture.ManagerUserId).GivenMetadata(metadata)
            .When(request).ExpectSuccess();
        fixture.DenyManagerRestrictedRead();

        // Act
        var replay = await fixture.As(fixture.ManagerUserId).GivenMetadata(metadata)
            .When(request).ExpectFailure(RequestErrorKind.NotFound);
        var changedReplay = await fixture.As(fixture.ManagerUserId).GivenMetadata(metadata)
            .When(request with { Name = "Changed campaign" })
            .ExpectFailure(RequestErrorKind.NotFound);

        // Assert
        Assert.Contains("not found", replay.Error!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not found", changedReplay.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldRejectChangedFrozenContentGivenIdempotentLaunchReplay()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        fixture.Sources.AccessOwner = fixture.ApproverMemberId;
        var assignment = new AccessReviewAssignment(populationId, fixture.ReviewerMemberId,
            "Assigned as the trained access-review delegate.");
        var request = new LaunchAccessReviewCampaign(fixture.TenantId, "Q3 AWS review",
            "Keep only access each person still needs.", DateTimeOffset.UtcNow.AddDays(14),
            [assignment], fixture.ProgramId, fixture.RemediationOwnerMemberId);
        var requestId = Uuid.CreateVersion4();
        var metadata = new RequestMetadata(requestId, requestId, null);
        var launched = (await fixture.As(fixture.ManagerUserId).GivenMetadata(metadata)
            .When(request).ExpectSuccess()).Value;
        var replay = (await fixture.As(fixture.ManagerUserId).GivenMetadata(metadata)
            .When(request).ExpectSuccess()).Value;
        var changedRequests = new[]
        {
            request with { Name = "Changed name" },
            request with { Instructions = "Changed instructions" },
            request with { Deadline = request.Deadline.AddDays(1) },
            request with
            {
                Assignments = [assignment with { ReviewerMemberId = fixture.ApproverMemberId }],
            },
            request with
            {
                Assignments = [assignment with { DelegationReason = "Changed delegation basis." }],
            },
            request with { Assignments = [assignment with { PopulationId = Uuid.CreateVersion4() }] },
            request with { ProgramId = Uuid.CreateVersion4() },
            request with { RemediationOwnerMemberId = Uuid.CreateVersion4() },
        };
        var aggregate = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new AccessReviewCampaign(fixture.TenantId, launched.CampaignId));
        var launch = aggregate.Launched!;
        var changedHash = launch.ContentSha256 == new string('0', 64)
            ? new string('1', 64)
            : new string('0', 64);
        var exactAggregateReplay = aggregate.Launch(launch.Name, launch.Instructions,
            launch.Deadline, Uuid.CreateVersion4(), launch.ContentSha256, launch.Reviewers,
            launch.Items, launch.LaunchedBy, launch.LaunchedAt, launch.ProgramId,
            launch.RemediationOwnerMemberId);
        var changedHashReplay = aggregate.Launch(launch.Name, launch.Instructions,
            launch.Deadline, launch.SnapshotId, changedHash, launch.Reviewers, launch.Items,
            launch.LaunchedBy, launch.LaunchedAt, launch.ProgramId,
            launch.RemediationOwnerMemberId);

        // Act
        var conflicts = new List<RequestError>();
        foreach (var changedRequest in changedRequests)
            conflicts.Add((await fixture.As(fixture.ManagerUserId).GivenMetadata(metadata)
                .When(changedRequest).ExpectFailure(RequestErrorKind.Conflict)).Error!);

        // Assert
        Assert.Equal(launched, replay);
        Assert.Equal(changedRequests.Length, conflicts.Count);
        Assert.All(conflicts, conflict => Assert.Contains("different", conflict.Message,
            StringComparison.OrdinalIgnoreCase));
        Assert.Equal(launched.SnapshotId, exactAggregateReplay.Value.SnapshotId);
        Assert.Equal(RequestErrorKind.Conflict, changedHashReplay.Error!.Kind);
    }

    [Fact]
    public async Task ShouldRequireDelegationReasonGivenReviewerOtherThanAccessOwner()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        var launch = new LaunchAccessReviewCampaign(fixture.TenantId, "Review", "Instructions",
            DateTimeOffset.UtcNow.AddDays(7),
            [new AccessReviewAssignment(populationId, fixture.ApproverMemberId)],
            fixture.ProgramId, fixture.RemediationOwnerMemberId);

        // Act
        var refused = await fixture.FailAsync(fixture.ManagerUserId, launch, RequestErrorKind.Validation);
        var delegated = await fixture.LaunchAsync(populationId, fixture.ApproverMemberId,
            "Access owner on leave.");
        var campaign = await fixture.CampaignAsync(delegated.CampaignId);

        // Assert
        Assert.Contains("delegat", refused.Message, StringComparison.Ordinal);
        Assert.True(Assert.Single(campaign.Reviewers).Delegated);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ShouldRejectCampaignLaunchGivenMissingOwnerProgramOrRemediationOwner(
        bool includeProgram, bool includeRemediationOwner)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        var launch = new LaunchAccessReviewCampaign(fixture.TenantId, "Q3 review", "Review access.",
            DateTimeOffset.UtcNow.AddDays(14),
            [new AccessReviewAssignment(populationId, fixture.ReviewerMemberId)],
            includeProgram ? fixture.ProgramId : null,
            includeRemediationOwner ? fixture.RemediationOwnerMemberId : null);

        // Act
        var refused = await fixture.FailAsync(fixture.ManagerUserId, launch, RequestErrorKind.Validation);

        // Assert
        Assert.Contains("owner Program", refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldRejectCampaignLaunchGivenReviewerLacksCurrentQueueRead()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        fixture.Permissions.Deny(fixture.ReviewerUserId,
            Bdgrz.Compliance.Features.Programs.IProgramReadRequest.ReadPermission);

        // Act
        var refused = await fixture.FailAsync(fixture.ManagerUserId,
            new LaunchAccessReviewCampaign(fixture.TenantId, "Q3 review", "Review access.",
                DateTimeOffset.UtcNow.AddDays(14),
                [new AccessReviewAssignment(populationId, fixture.ReviewerMemberId)],
                fixture.ProgramId, fixture.RemediationOwnerMemberId), RequestErrorKind.Validation);

        // Assert
        Assert.Contains("program queue", refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldRejectCampaignLaunchGivenRemediationOwnerLacksAccessReviewPermission()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        fixture.Permissions.Deny(fixture.ManagerUserId,
            Bdgrz.Compliance.Features.AccessControl.RbacPermissions.AccessReviewManage);

        // Act
        var refused = await fixture.FailAsync(fixture.ApproverUserId,
            new LaunchAccessReviewCampaign(fixture.TenantId, "Q3 review", "Review access.",
                DateTimeOffset.UtcNow.AddDays(14),
                [new AccessReviewAssignment(populationId, fixture.ReviewerMemberId)],
                fixture.ProgramId, fixture.RemediationOwnerMemberId), RequestErrorKind.Validation);

        // Assert
        Assert.Contains("remediation owner", refused.Message, StringComparison.OrdinalIgnoreCase);
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
    public async Task ShouldReassignFrozenReviewerOnlyAfterEligibilityLossGivenRoutedCampaign()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        fixture.Sources.AccessOwner = fixture.ApproverMemberId;
        var launched = await fixture.LaunchAsync(populationId, fixture.ApproverMemberId);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var itemId = ItemId(campaign, "ada", "deploy");
        var request = new ReassignAccessReviewResponsibility(fixture.TenantId, launched.CampaignId,
            itemId, AccessReviewResponsibilityKind.Reviewer, 1, fixture.ReviewerMemberId,
            "Original reviewer lost current queue access.",
            "Appointed a trained member as a delegate for this system.");

        // Act
        var eligibleOwner = await fixture.FailAsync(fixture.ManagerUserId, request,
            RequestErrorKind.Conflict);
        fixture.Permissions.Deny(fixture.ApproverUserId,
            Bdgrz.Compliance.Features.Programs.IProgramReadRequest.ReadPermission);
        var missingDelegation = await fixture.FailAsync(fixture.ManagerUserId,
            request with { DelegationReason = null }, RequestErrorKind.Validation);
        var requestId = Uuid.CreateVersion4();
        var metadata = new RequestMetadata(requestId, requestId, null);
        var reassignment = (await fixture.As(fixture.ManagerUserId).GivenMetadata(metadata)
            .When(request).ExpectSuccess()).Value;
        var replay = (await fixture.As(fixture.ManagerUserId).GivenMetadata(metadata)
            .When(request).ExpectSuccess()).Value;
        var conflictingReplay = await fixture.As(fixture.ManagerUserId).GivenMetadata(metadata)
            .When(request with { DelegationReason = "Different delegate basis." })
            .ExpectFailure(RequestErrorKind.Conflict);
        var reassigned = await fixture.CampaignAsync(launched.CampaignId);
        var oldReviewerDecision = await fixture.FailAsync(fixture.ApproverUserId,
            new RecordAccessDecision(fixture.TenantId, launched.CampaignId, itemId, 2,
                "keep", "Still required."), RequestErrorKind.NotFound);
        var aggregate = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new AccessReviewCampaign(fixture.TenantId, launched.CampaignId));
        var staleRevision = aggregate.ReassignResponsibility(itemId,
            AccessReviewResponsibilityKind.Reviewer, 1, Uuid.CreateVersion4(),
            fixture.ApproverMemberId, "Revision changed.",
            ActorReference.ForMember(fixture.ManagerMemberId, "Manager"), DateTimeOffset.UtcNow,
            "The delegate has the current system access owner authority.");

        // Assert
        Assert.Contains("loses eligibility", eligibleOwner.Message, StringComparison.Ordinal);
        Assert.Contains("delegate", missingDelegation.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(fixture.ApproverMemberId, reassignment.PreviousMemberId);
        Assert.Equal(fixture.ReviewerMemberId, reassignment.AssignedMemberId);
        Assert.Equal(reassignment, replay);
        Assert.Equal(RequestErrorKind.Conflict, conflictingReplay.Error!.Kind);
        var state = Assert.Single(reassigned.Items, item => item.Item.ItemId == itemId);
        Assert.Equal(fixture.ApproverMemberId, state.Item.ReviewerMemberId);
        Assert.Equal(fixture.ReviewerMemberId, state.CurrentReviewerMemberId);
        Assert.Equal(reassignment, Assert.Single(state.ResponsibilityReassignments!));
        Assert.Equal("Appointed a trained member as a delegate for this system.",
            reassignment.DelegationReason);
        Assert.Equal(RequestErrorKind.Conflict, staleRevision.Error!.Kind);
        Assert.NotNull(oldReviewerDecision);
    }

    [Fact]
    public async Task ShouldRequireCurrentRemediationOwnerAfterAuditedReassignmentGivenLostEligibility()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId, fixture.ApproverMemberId,
            "Remediation test reviewer.");
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var itemId = ItemId(campaign, "ada", "deploy");
        await fixture.SendAsync(fixture.ApproverUserId, new RecordAccessDecision(fixture.TenantId,
            launched.CampaignId, itemId, 1, "revoke", "No longer required."));
        fixture.Permissions.Deny(fixture.ManagerUserId,
            Bdgrz.Compliance.Features.AccessControl.RbacPermissions.AccessReviewManage);
        var reassign = new ReassignAccessReviewResponsibility(fixture.TenantId, launched.CampaignId,
            itemId, AccessReviewResponsibilityKind.RemediationOwner, 2,
            fixture.ReviewerMemberId, "Original owner lost access-review authority.");

        // Act
        var assignment = await fixture.SendAsync(fixture.ApproverUserId, reassign);
        var oldOwnerChange = await fixture.FailAsync(fixture.ApproverUserId,
            new RecordAccessRemediationChange(fixture.TenantId, launched.CampaignId, itemId, 3,
                "ticket-1", "Disable access.", DateTimeOffset.UtcNow), RequestErrorKind.NotFound);
        var change = await fixture.SendAsync(fixture.ReviewerUserId,
            new RecordAccessRemediationChange(fixture.TenantId, launched.CampaignId, itemId, 3,
                "ticket-1", "Disable access.", DateTimeOffset.UtcNow));
        var updated = await fixture.CampaignAsync(launched.CampaignId, fixture.ApproverUserId);

        // Assert
        Assert.Equal(fixture.ManagerMemberId, assignment.PreviousMemberId);
        Assert.Equal(fixture.ReviewerMemberId, assignment.AssignedMemberId);
        Assert.NotNull(oldOwnerChange);
        var item = Assert.Single(updated.Items, state => state.Item.ItemId == itemId);
        Assert.Equal(fixture.ManagerMemberId, updated.RemediationOwnerMemberId);
        Assert.Equal(fixture.ReviewerMemberId, item.CurrentRemediationOwnerMemberId);
        Assert.Equal(fixture.ReviewerMemberId.ToString(), change.RecordedBy.Id);
    }

    [Fact]
    public async Task ShouldKeepLegacyCampaignTenantDirectGivenNoProgramQueueReadGrant()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var routed = await fixture.LaunchAsync(populationId);
        var (campaignId, itemId) = await fixture.SeedLegacyCampaignAsync(routed.CampaignId);
        fixture.Permissions.Deny(fixture.ReviewerUserId,
            Bdgrz.Compliance.Features.Programs.IProgramReadRequest.ReadPermission);
        fixture.Permissions.Deny(fixture.ManagerUserId,
            Bdgrz.Compliance.Features.Programs.IProgramReadRequest.ReadPermission);

        // Act
        var visible = await fixture.CampaignAsync(campaignId, fixture.ReviewerUserId);
        var decision = await fixture.SendAsync(fixture.ReviewerUserId,
            new RecordAccessDecision(fixture.TenantId, campaignId, itemId, 1, "revoke",
                "No longer required."));
        var change = await fixture.SendAsync(fixture.ManagerUserId,
            new RecordAccessRemediationChange(fixture.TenantId, campaignId, itemId, 2,
                "ticket-legacy", "Disable the old grant.", DateTimeOffset.UtcNow));

        // Assert
        Assert.Equal(fixture.ReviewerMemberId, Assert.Single(visible.Items,
            item => item.Item.ItemId == itemId).CurrentReviewerMemberId);
        Assert.Equal(fixture.ReviewerMemberId, decision.ReviewerMemberId);
        Assert.Equal(fixture.ManagerMemberId.ToString(), change.RecordedBy.Id);
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
