using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class PolicyDecisionWorkTests
{
    [Fact]
    public async Task ShouldShowPendingDraftReviewToIndependentProgramManagerGivenPolicyDraft()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var policyId = Uuid.CreateVersion4();
        await SeedDraftAsync(fixture, policyId, fixture.LeadMemberId);

        // Act
        var managerQueue = await WorkAsync(fixture, fixture.ApproverUserId);
        var repeatedQueue = await WorkAsync(fixture, fixture.ApproverUserId);
        var authorQueue = await WorkAsync(fixture, fixture.LeadUserId);
        var outsiderQueue = await WorkAsync(fixture, fixture.OutsiderUserId);

        // Assert
        var item = Assert.Single(managerQueue.Items);
        Assert.Equal("policy_draft_review", item.Kind);
        Assert.Equal(policyId, item.SourceId);
        Assert.Contains("revision 1", item.Reason, StringComparison.Ordinal);
        Assert.Equal("review", item.NextAction);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"policies/{policyId}/reviews", item.ActionPath);
        Assert.Equal(item.WorkItemId, Assert.Single(repeatedQueue.Items).WorkItemId);
        Assert.Empty(authorQueue.Items);
        Assert.Empty(outsiderQueue.Items);
    }

    [Fact]
    public async Task ShouldRouteAcceptedDraftToIndependentApprovalGivenAcceptedReview()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        fixture.Permissions.Managers.Add(fixture.ReviewerMemberId);
        var policyId = Uuid.CreateVersion4();
        await SeedDraftAsync(fixture, policyId, fixture.LeadMemberId);
        var reviewWork = Assert.Single((await WorkAsync(fixture, fixture.ApproverUserId)).Items);
        var reviewId = Uuid.CreateVersion4();
        await ApplyPolicyAsync(fixture, policyId, policy =>
        {
            Assert.Null(policy.Review(fixture.ProgramId, policy.Revision, reviewId, "accept",
                "The draft is ready.", Actor(fixture.ReviewerMemberId), fixture.ReviewerMemberId,
                At(fixture.Today).AddMinutes(1)));
            return Result.Success;
        });
        await RefreshProjectionAsync(fixture, policyId);

        // Act
        var approvalQueue = await WorkAsync(fixture, fixture.ApproverUserId);
        var authorQueue = await WorkAsync(fixture, fixture.LeadUserId);
        var reviewerQueue = await WorkAsync(fixture, fixture.ReviewerUserId);

        // Assert
        var item = Assert.Single(approvalQueue.Items);
        Assert.Equal("policy_draft_approval", item.Kind);
        Assert.Equal("approve", item.NextAction);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"policies/{policyId}/approvals", item.ActionPath);
        Assert.NotEqual(reviewWork.WorkItemId, item.WorkItemId);
        Assert.Empty(authorQueue.Items);
        Assert.Empty(reviewerQueue.Items);

        // Complete the source workflow and verify the decision work disappears.
        await ApplyPolicyAsync(fixture, policyId, policy =>
        {
            Assert.Null(policy.Approve(fixture.ProgramId, policy.Revision, Uuid.CreateVersion4(),
                reviewId, fixture.Today, true, "Approved.", null,
                Actor(fixture.ApproverMemberId), fixture.ApproverMemberId,
                At(fixture.Today).AddMinutes(2)));
            return Result.Success;
        });
        await RefreshProjectionAsync(fixture, policyId);
        Assert.Empty((await WorkAsync(fixture, fixture.ApproverUserId)).Items);
    }

    [Fact]
    public async Task ShouldTransitionRetirementReviewToIndependentApprovalGivenAcceptedReview()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        fixture.Permissions.Managers.Add(fixture.ReviewerMemberId);
        var policyId = Uuid.CreateVersion4();
        await SeedApprovedPolicyAsync(fixture, policyId, fixture.Today.AddDays(-40), 12);
        await ApplyPolicyAsync(fixture, policyId, policy =>
        {
            Assert.Null(policy.ProposeRetirement(fixture.ProgramId, policy.CurrentVersion!.Version,
                fixture.Today.AddDays(30), "The policy is replaced.", Actor(fixture.LeadMemberId),
                fixture.LeadMemberId, At(fixture.Today)));
            return Result.Success;
        });
        await RefreshProjectionAsync(fixture, policyId);
        var reviewWork = Assert.Single((await WorkAsync(fixture, fixture.ApproverUserId)).Items,
            item => item.Kind == "policy_retirement_review");
        await ApplyPolicyAsync(fixture, policyId, policy =>
        {
            Assert.Null(policy.Review(fixture.ProgramId, policy.Revision, Uuid.CreateVersion4(),
                "accept", "Retirement is appropriate.", Actor(fixture.ReviewerMemberId),
                fixture.ReviewerMemberId, At(fixture.Today).AddMinutes(1)));
            return Result.Success;
        });
        await RefreshProjectionAsync(fixture, policyId);

        // Act
        var approvalQueue = await WorkAsync(fixture, fixture.ApproverUserId);
        var proposerQueue = await WorkAsync(fixture, fixture.LeadUserId);
        var reviewerQueue = await WorkAsync(fixture, fixture.ReviewerUserId);

        // Assert
        var item = Assert.Single(approvalQueue.Items,
            candidate => candidate.Kind == "policy_retirement_approval");
        Assert.Equal("approve", item.NextAction);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"policies/{policyId}/retirements", item.ActionPath);
        Assert.NotEqual(reviewWork.WorkItemId, item.WorkItemId);
        Assert.Empty(proposerQueue.Items);
        Assert.Empty(reviewerQueue.Items);
    }

    [Fact]
    public async Task ShouldQueueDuePeriodicReviewAndRemoveItAfterConfirmationGivenApprovedPolicy()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var policyId = Uuid.CreateVersion4();
        await SeedApprovedPolicyAsync(fixture, policyId, fixture.Today.AddMonths(-1), 1);

        // Act
        var dueQueue = await WorkAsync(fixture, fixture.ApproverUserId);
        var item = Assert.Single(dueQueue.Items);
        await ApplyPolicyAsync(fixture, policyId, policy =>
        {
            Assert.Null(policy.ConfirmPeriodicReview(fixture.ProgramId,
                policy.CurrentVersion!.Version, Uuid.CreateVersion4(), "Reviewed this period.",
                Actor(fixture.ApproverMemberId), fixture.ApproverMemberId,
                At(fixture.Today)));
            return Result.Success;
        });
        await RefreshProjectionAsync(fixture, policyId);

        // Assert
        Assert.Equal("policy_periodic_review", item.Kind);
        Assert.Equal(fixture.Today, item.DueOn);
        Assert.Equal("confirm_review", item.NextAction);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"policies/{policyId}/periodic-reviews", item.ActionPath);
        Assert.Empty((await WorkAsync(fixture, fixture.ApproverUserId)).Items);
    }

    [Fact]
    public async Task ShouldCreateNewPeriodicReviewIdentityGivenNextReviewCycle()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var policyId = Uuid.CreateVersion4();
        await SeedApprovedPolicyAsync(fixture, policyId, fixture.Today.AddMonths(-1), 1);
        var first = Assert.Single((await WorkAsync(fixture, fixture.ApproverUserId, 365)).Items);
        await ApplyPolicyAsync(fixture, policyId, policy =>
        {
            Assert.Null(policy.ConfirmPeriodicReview(fixture.ProgramId,
                policy.CurrentVersion!.Version, Uuid.CreateVersion4(), "Reviewed this period.",
                Actor(fixture.ApproverMemberId), fixture.ApproverMemberId,
                At(fixture.Today)));
            return Result.Success;
        });
        await RefreshProjectionAsync(fixture, policyId);

        // Act
        var next = Assert.Single((await WorkAsync(fixture, fixture.ApproverUserId, 365)).Items);

        // Assert
        Assert.Equal(fixture.Today.AddMonths(1), next.DueOn);
        Assert.NotEqual(first.WorkItemId, next.WorkItemId);
    }

    [Fact]
    public async Task ShouldKeepPolicyDecisionWorkWithinRequestedTenantAndProgramGivenDraft()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var policyId = Uuid.CreateVersion4();
        await SeedDraftAsync(fixture, policyId, fixture.LeadMemberId);

        // Act
        var ownQueue = await WorkAsync(fixture, fixture.ApproverUserId);
        var otherProgramQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, Uuid.CreateVersion4(), "unassigned"));
        var otherTenantQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(Uuid.CreateVersion4(), fixture.ProgramId, "unassigned"));

        // Assert
        Assert.Single(ownQueue.Items, item => item.Kind == "policy_draft_review");
        Assert.Empty(otherProgramQueue.Items);
        Assert.Empty(otherTenantQueue.Items);
    }

    [Fact]
    public async Task ShouldCreateNewWorkIdentityGivenDraftRevisionChanges()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var policyId = Uuid.CreateVersion4();
        await SeedDraftAsync(fixture, policyId, fixture.LeadMemberId);
        var first = Assert.Single((await WorkAsync(fixture, fixture.ApproverUserId)).Items);

        // Act
        await ApplyPolicyAsync(fixture, policyId, policy =>
        {
            Assert.Null(policy.Revise(fixture.ProgramId, policy.Revision,
                Content("Revised Access Control Policy"), Actor(fixture.LeadMemberId),
                fixture.LeadMemberId, At(fixture.Today).AddMinutes(1)));
            return Result.Success;
        });
        await RefreshProjectionAsync(fixture, policyId);
        var revised = Assert.Single((await WorkAsync(fixture, fixture.ApproverUserId)).Items);

        // Assert
        Assert.Equal("policy_draft_review", revised.Kind);
        Assert.Contains("revision 2", revised.Reason, StringComparison.Ordinal);
        Assert.NotEqual(first.WorkItemId, revised.WorkItemId);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenPolicyProjectionAheadOfSource()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var policyId = Uuid.CreateVersion4();
        await SeedDraftAsync(fixture, policyId, fixture.LeadMemberId);
        var projected = fixture.Policies.Find(policyId)!;
        fixture.Policies.Add(projected with { PendingStatus = null });

        // Act
        var stale = await fixture.Scenario(fixture.ApproverUserId)
            .When(new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"))
            .ExpectFailure(RequestErrorKind.Conflict);

        // Assert
        Assert.True(stale.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldOmitPeriodicReviewBeyondQueueHorizonGivenFutureDueDate()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await SeedApprovedPolicyAsync(fixture, Uuid.CreateVersion4(), fixture.Today, 1);

        // Act
        var queue = await WorkAsync(fixture, fixture.ApproverUserId);

        // Assert
        Assert.Empty(queue.Items);
    }

    static Task<WorkQueueView> WorkAsync(OperationsFixture fixture, Uuid userId,
        int? horizonDays = null) =>
        fixture.AsAsync(userId, new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned",
            horizonDays));

    static Task SeedDraftAsync(OperationsFixture fixture, Uuid policyId, Uuid authorMemberId) =>
        SeedDraftAsync(fixture, policyId, authorMemberId, 12);

    static async Task SeedDraftAsync(OperationsFixture fixture, Uuid policyId, Uuid authorMemberId,
        int cadenceMonths)
    {
        await ApplyPolicyAsync(fixture, policyId, policy =>
        {
            Assert.True(policy.Create(fixture.ProgramId, Uuid.CreateVersion4(), "POL-WORK",
                Content(cadenceMonths: cadenceMonths), Actor(authorMemberId), authorMemberId,
                At(fixture.Today)).IsSuccess);
            return Result.Success;
        });
        await RefreshProjectionAsync(fixture, policyId);
    }

    static async Task SeedApprovedPolicyAsync(OperationsFixture fixture, Uuid policyId,
        DateOnly approvedOn, int cadenceMonths)
    {
        await SeedDraftAsync(fixture, policyId, fixture.LeadMemberId, cadenceMonths);
        var reviewId = Uuid.CreateVersion4();
        await ApplyPolicyAsync(fixture, policyId, policy =>
        {
            Assert.Null(policy.Review(fixture.ProgramId, policy.Revision, reviewId, "accept",
                "The draft is ready.", Actor(fixture.ReviewerMemberId), fixture.ReviewerMemberId,
                At(approvedOn).AddMinutes(1)));
            Assert.Null(policy.Approve(fixture.ProgramId, policy.Revision, Uuid.CreateVersion4(),
                reviewId, approvedOn, true, "Approve the policy.", null,
                Actor(fixture.ApproverMemberId), fixture.ApproverMemberId,
                At(approvedOn).AddMinutes(2)));
            return Result.Success;
        });
        await RefreshProjectionAsync(fixture, policyId);
    }

    static async Task ApplyPolicyAsync(OperationsFixture fixture, Uuid policyId,
        Func<Policy, Result> apply) =>
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new Policy(fixture.TenantId, policyId), apply);

    static async Task RefreshProjectionAsync(OperationsFixture fixture, Uuid policyId)
    {
        var policy = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new Policy(fixture.TenantId, policyId));
        fixture.Policies.Add(policy.ToView(fixture.Today) is { } view
            ? new PolicySummaryView(view.TenantId, view.ProgramId, view.PolicyId, view.Identifier,
                view.Draft?.Title ?? policy.CurrentVersion?.Content.Title ?? view.Identifier,
                view.Status, view.PendingStatus, view.Revision, view.CurrentVersion,
                view.CurrentEffectiveFrom, view.NextReviewDueOn, view.ReviewOverdue,
                view.LastChangedAt)
            : throw new InvalidOperationException("The test policy is not visible."));
    }

    static PolicyContent Content(string title = "Access Control Policy", int cadenceMonths = 12) =>
        new(title, "Govern access", PolicyAudience.CoreSecurity, null, cadenceMonths, "Body", null,
            "Security owner", []);

    static ActorReference Actor(Uuid memberId) => ActorReference.ForMember(memberId,
        memberId.ToString());

    static DateTimeOffset At(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
