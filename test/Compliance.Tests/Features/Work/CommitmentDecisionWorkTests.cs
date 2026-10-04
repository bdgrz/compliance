using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class CommitmentDecisionWorkTests
{
    static readonly string SourceReference = "MSA 4.1";

    [Fact]
    public async Task ShouldQueuePendingReviewForProgramReviewersGivenCommitmentDraft()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var draftId = Uuid.CreateVersion4();
        await SeedDraftAsync(fixture, draftId);

        // Act
        var queue = await QueueAsync(fixture, fixture.ApproverUserId);
        var repeated = await QueueAsync(fixture, fixture.ApproverUserId);
        var authorQueue = await QueueAsync(fixture, fixture.LeadUserId);
        var outsiderQueue = await QueueAsync(fixture, fixture.OutsiderUserId);

        // Assert
        var item = Assert.Single(queue.Items);
        Assert.Equal("commitment_draft_review", item.Kind);
        Assert.Equal(draftId, item.SourceId);
        Assert.Equal("review", item.NextAction);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"commitment-drafts/{draftId}/reviews", item.ActionPath);
        Assert.Equal(item.WorkItemId, Assert.Single(repeated.Items).WorkItemId);
        Assert.Empty(authorQueue.Items);
        Assert.Empty(outsiderQueue.Items);
    }

    [Fact]
    public async Task ShouldRouteAcceptedReviewToIndependentApproverGivenCurrentRevision()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        fixture.Permissions.Managers.Add(fixture.ReviewerMemberId);
        var draftId = Uuid.CreateVersion4();
        await SeedDraftAsync(fixture, draftId);
        var reviewId = Uuid.CreateVersion4();
        await ApplyDraftAsync(fixture, draftId, draft =>
        {
            Assert.Null(draft.Review(fixture.ProgramId, draft.Revision, reviewId, "accept",
                "Security lead", "applicable", "supported", null, "Verified against source",
                fixture.ReviewerMemberId, "Reviewer", At(fixture.Today).AddMinutes(1), null,
                SourceReference, "Signed MSA section 4.1"));
            return Result.Success;
        });
        await RefreshDirectoryAsync(fixture, draftId);

        // Act
        var approvalQueue = await QueueAsync(fixture, fixture.ApproverUserId);
        var authorQueue = await QueueAsync(fixture, fixture.LeadUserId);
        var reviewerQueue = await QueueAsync(fixture, fixture.ReviewerUserId);

        // Assert
        var item = Assert.Single(approvalQueue.Items);
        Assert.Equal("commitment_draft_approval", item.Kind);
        Assert.Equal("approve", item.NextAction);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"commitment-drafts/{draftId}/approvals", item.ActionPath);
        Assert.Empty(authorQueue.Items);
        Assert.Empty(reviewerQueue.Items);

        // Complete the authoritative source decision; the queue cannot complete it itself.
        await ApplyDraftAsync(fixture, draftId, draft =>
        {
            Assert.Null(draft.Approve(fixture.ProgramId, draft.Revision, Uuid.CreateVersion4(),
                reviewId, fixture.Today, "Approved", "impact-digest", fixture.ApproverMemberId,
                "Approver", At(fixture.Today).AddMinutes(2)));
            return Result.Success;
        });
        await RefreshDirectoryAsync(fixture, draftId);
        Assert.Empty((await QueueAsync(fixture, fixture.ApproverUserId)).Items);
    }

    [Fact]
    public async Task ShouldQueueEachActiveReviewerGivenMultipleExactRevisionAssignments()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var draftId = Uuid.CreateVersion4();
        await SeedDraftAsync(fixture, draftId);
        await ApplyDraftAsync(fixture, draftId, draft =>
        {
            Assign(draft, fixture, fixture.ReviewerMemberId, ResponsibilityType.AssignedReviewer);
            Assign(draft, fixture, fixture.OwnerMemberId, ResponsibilityType.AssignedReviewer);
            return Result.Success;
        });
        await RefreshDirectoryAsync(fixture, draftId);

        // Act
        var reviewerQueue = await QueueAsync(fixture, fixture.ReviewerUserId, "mine");
        var secondReviewerQueue = await QueueAsync(fixture, fixture.OwnerUserId, "mine");
        var authorQueue = await QueueAsync(fixture, fixture.LeadUserId, "mine");

        // Assert
        var reviewerItem = Assert.Single(reviewerQueue.Items);
        var secondItem = Assert.Single(secondReviewerQueue.Items);
        Assert.Equal("commitment_draft_review", reviewerItem.Kind);
        Assert.Equal("commitment_draft_review", secondItem.Kind);
        Assert.NotEqual(reviewerItem.WorkItemId, secondItem.WorkItemId);
        Assert.Equal(fixture.ReviewerMemberId, reviewerItem.AssigneeMemberId);
        Assert.Equal(fixture.OwnerMemberId, secondItem.AssigneeMemberId);
        Assert.Empty(authorQueue.Items);
    }

    [Fact]
    public async Task ShouldRouteApprovalToEachActiveApproverGivenAcceptedReview()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var draftId = Uuid.CreateVersion4();
        await SeedDraftAsync(fixture, draftId);
        await ApplyDraftAsync(fixture, draftId, draft =>
        {
            Assign(draft, fixture, fixture.OwnerMemberId, ResponsibilityType.PolicyApprover);
            Assign(draft, fixture, fixture.BackupMemberId, ResponsibilityType.PolicyApprover);
            Assert.Null(draft.Review(fixture.ProgramId, draft.Revision, Uuid.CreateVersion4(),
                "accept", "Security lead", "applicable", "supported", null,
                "Verified against source", fixture.ReviewerMemberId, "Reviewer",
                At(fixture.Today).AddMinutes(1), null, SourceReference,
                "Signed MSA section 4.1"));
            return Result.Success;
        });
        await RefreshDirectoryAsync(fixture, draftId);

        // Act
        var firstQueue = await QueueAsync(fixture, fixture.OwnerUserId, "mine");
        var secondQueue = await QueueAsync(fixture, fixture.BackupUserId, "mine");

        // Assert
        var first = Assert.Single(firstQueue.Items);
        var second = Assert.Single(secondQueue.Items);
        Assert.Equal("commitment_draft_approval", first.Kind);
        Assert.Equal("commitment_draft_approval", second.Kind);
        Assert.NotEqual(first.WorkItemId, second.WorkItemId);
        Assert.Equal(fixture.OwnerMemberId, first.AssigneeMemberId);
        Assert.Equal(fixture.BackupMemberId, second.AssigneeMemberId);
    }

    [Fact]
    public async Task ShouldUseProgramReviewerFallbackGivenOnlyAssignmentForPriorRevision()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var draftId = Uuid.CreateVersion4();
        await SeedDraftAsync(fixture, draftId);
        await ApplyDraftAsync(fixture, draftId, draft =>
        {
            Assign(draft, fixture, fixture.ReviewerMemberId, ResponsibilityType.AssignedReviewer);
            Assert.Null(draft.Revise(fixture.ProgramId, draft.Revision, "Updated statement",
                "Context", SourceReference, fixture.LeadMemberId, "Lead",
                At(fixture.Today).AddMinutes(1)));
            return Result.Success;
        });
        await RefreshDirectoryAsync(fixture, draftId);

        // Act
        var managerQueue = await QueueAsync(fixture, fixture.ApproverUserId);
        var priorReviewerQueue = await QueueAsync(fixture, fixture.ReviewerUserId, "mine");

        // Assert
        var item = Assert.Single(managerQueue.Items);
        Assert.Equal("commitment_draft_review", item.Kind);
        Assert.Contains("revision 2", item.Reason, StringComparison.Ordinal);
        Assert.Empty(priorReviewerQueue.Items);
    }

    [Fact]
    public async Task ShouldUseProgramReviewerFallbackGivenRevokedReviewerAssignment()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var draftId = Uuid.CreateVersion4();
        await SeedDraftAsync(fixture, draftId);
        var assignmentId = Uuid.CreateVersion4();
        await ApplyDraftAsync(fixture, draftId, draft =>
        {
            var scope = new ResponsibilityScope("commitment", draft.Id, draft.Id,
                draft.Revision);
            var assignedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
            Assert.Null(draft.AssignResponsibility(scope, assignmentId, fixture.ReviewerMemberId,
                ResponsibilityType.AssignedReviewer, fixture.LeadMemberId, "Lead", assignedAt,
                assignedAt, null, []));
            Assert.Null(draft.RevokeResponsibility(scope, assignmentId, fixture.ReviewerMemberId,
                "Reviewer", DateTimeOffset.UtcNow.AddHours(1), "Reviewer changed."));
            return Result.Success;
        });
        await RefreshDirectoryAsync(fixture, draftId);

        // Act
        var managerQueue = await QueueAsync(fixture, fixture.ApproverUserId);
        var revokedReviewerQueue = await QueueAsync(fixture, fixture.ReviewerUserId, "mine");

        // Assert
        Assert.Single(managerQueue.Items);
        Assert.Empty(revokedReviewerQueue.Items);
    }

    [Fact]
    public async Task ShouldReturnToReviewAndChangeIdentityGivenRequestedChangesThenRevision()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var draftId = Uuid.CreateVersion4();
        await SeedDraftAsync(fixture, draftId);
        var initial = Assert.Single((await QueueAsync(fixture, fixture.ApproverUserId)).Items);
        await ApplyDraftAsync(fixture, draftId, draft =>
        {
            Assert.Null(draft.Review(fixture.ProgramId, draft.Revision, Uuid.CreateVersion4(),
                "request_changes", null, null, null, null, "Clarify the source mapping",
                fixture.ReviewerMemberId, "Reviewer", At(fixture.Today).AddMinutes(1)));
            return Result.Success;
        });
        await RefreshDirectoryAsync(fixture, draftId);

        // Act
        var afterChanges = Assert.Single((await QueueAsync(fixture, fixture.ApproverUserId)).Items);
        await ApplyDraftAsync(fixture, draftId, draft =>
        {
            Assert.Null(draft.Revise(fixture.ProgramId, draft.Revision, "Updated statement",
                "Context", SourceReference, fixture.LeadMemberId, "Lead",
                At(fixture.Today).AddMinutes(2)));
            return Result.Success;
        });
        await RefreshDirectoryAsync(fixture, draftId);
        var revised = Assert.Single((await QueueAsync(fixture, fixture.ApproverUserId)).Items);

        // Assert
        Assert.Equal("commitment_draft_review", afterChanges.Kind);
        Assert.Equal(initial.WorkItemId, afterChanges.WorkItemId);
        Assert.Equal("commitment_draft_review", revised.Kind);
        Assert.Contains("revision 2", revised.Reason, StringComparison.Ordinal);
        Assert.NotEqual(initial.WorkItemId, revised.WorkItemId);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenCommitmentProjectionChangesDuringRead()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await SeedDraftAsync(fixture, Uuid.CreateVersion4());
        fixture.Commitments.CheckpointAfterNextList = new ProjectionCheckpoint(
            new EventCursor("advanced"));
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var authority = services.GetRequiredService<OperatingAuthority>();
        var consistency = new CommitmentDraftListReadConsistency(fixture.Commitments,
            new InMemoryEventStore());
        var queue = new WorkQueueReader(reader, authority, TimeProvider.System,
            commitments: fixture.Commitments, commitmentConsistency: consistency);
        var actor = new OperationsActor(fixture.ApproverUserId, fixture.ApproverMemberId,
            "Approver");

        // Act
        var stale = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId, actor, 30,
            CancellationToken.None);

        // Assert
        Assert.False(stale.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error!.Kind);
        Assert.True(stale.Error.IsTransient);
    }

    static Task<WorkQueueView> QueueAsync(OperationsFixture fixture, Uuid userId,
        string scope = "unassigned") =>
        fixture.AsAsync(userId, new ListWork(fixture.TenantId, fixture.ProgramId, scope));

    static async Task SeedDraftAsync(OperationsFixture fixture, Uuid draftId)
    {
        await ApplyDraftAsync(fixture, draftId, draft =>
        {
            Assert.True(draft.Create(fixture.ProgramId, Uuid.CreateVersion4(),
                Uuid.CreateVersion4(), "service_commitment", "SC-WORK",
                "The service protects customer data.", "Security commitment.", SourceReference,
                fixture.LeadMemberId, "Lead", At(fixture.Today)).IsSuccess);
            return Result.Success;
        });
        await RefreshDirectoryAsync(fixture, draftId);
    }

    static void Assign(CommitmentDraft draft, OperationsFixture fixture, Uuid memberId,
        ResponsibilityType type)
    {
        var now = At(fixture.Today);
        Assert.Null(draft.AssignResponsibility(new ResponsibilityScope("commitment", draft.Id,
                draft.Id, draft.Revision), Uuid.CreateVersion4(), memberId, type,
            fixture.LeadMemberId, "Lead", now, now.AddMinutes(-1), null, []));
    }

    static async Task ApplyDraftAsync(OperationsFixture fixture, Uuid draftId,
        Func<CommitmentDraft, Result> apply) =>
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new CommitmentDraft(fixture.TenantId, draftId), apply);

    static async Task RefreshDirectoryAsync(OperationsFixture fixture, Uuid draftId)
    {
        var draft = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new CommitmentDraft(fixture.TenantId, draftId));
        fixture.Commitments.Add(new CommitmentDraftView(fixture.TenantId, draft.ProgramId,
            draft.Id, draft.ServiceId, draft.Kind!, draft.Identifier!, draft.Revision,
            draft.AcceptedReviewDecisionId is null ? "draft" : "reviewed", "verified",
            "verified", "applicable", "Statement", "Context", SourceReference,
            fixture.LeadMemberId, "Lead", At(fixture.Today)));
    }

    static DateTimeOffset At(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
