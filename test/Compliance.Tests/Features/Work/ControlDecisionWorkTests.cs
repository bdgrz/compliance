using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class ControlDecisionWorkTests
{
    [Fact]
    public async Task ShouldQueueExactDraftReviewForAssignedReviewerGivenOpenControl()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        fixture.Permissions.Managers.Add(fixture.ReviewerMemberId);
        var controlId = await CreateDraftAsync(fixture, assignReviewer: true);

        // Act
        var reviewerQueue = await QueueAsync(fixture, fixture.ReviewerUserId, "mine");
        var authorQueue = await QueueAsync(fixture, fixture.LeadUserId, "mine");

        // Assert
        var item = Assert.Single(reviewerQueue.Items);
        Assert.Equal("control_draft_review", item.Kind);
        Assert.Equal(ControlVersionIds.Initial(controlId), item.SourceId);
        Assert.Equal(controlId, item.ControlId);
        Assert.Equal("review", item.NextAction);
        Assert.Equal(new OperatingHolder(OperatingAuthority.MemberHolder,
            fixture.ReviewerMemberId), item.Responsible);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"controls/{controlId}/draft/reviews", item.ActionPath);
        Assert.Empty(authorQueue.Items);
    }

    [Fact]
    public async Task ShouldRouteAcceptedReviewToIndependentApproverGivenCurrentControlDraft()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        fixture.Permissions.Managers.Add(fixture.ReviewerMemberId);
        var controlId = await CreateDraftAsync(fixture, assignReviewer: true,
            assignApprover: true, assignOwner: true);
        var reviewId = Uuid.CreateVersion4();
        await ApplyControlAsync(fixture, controlId, control =>
        {
            Assert.Null(control.Review(fixture.ProgramId, control.Revision, reviewId, "accept",
                "Approved design.", fixture.ReviewerMemberId, "Reviewer", DateTimeOffset.UtcNow));
            return Result.Success;
        });

        // Act
        var approverQueue = await QueueAsync(fixture, fixture.ApproverUserId, "mine");
        var reviewerQueue = await QueueAsync(fixture, fixture.ReviewerUserId, "mine");
        var authorQueue = await QueueAsync(fixture, fixture.LeadUserId, "mine");

        // Assert
        var item = Assert.Single(approverQueue.Items);
        Assert.Equal("control_draft_approval", item.Kind);
        Assert.Equal(ControlVersionIds.Initial(controlId), item.SourceId);
        Assert.Equal("approve", item.NextAction);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"controls/{controlId}/draft/approvals", item.ActionPath);
        Assert.Empty(reviewerQueue.Items);
        Assert.Empty(authorQueue.Items);

        // Complete the source decision; the queue cannot complete it itself.
        await ApplyControlAsync(fixture, controlId, control =>
        {
            Assert.Null(control.Approve(fixture.ProgramId, control.Revision,
                Uuid.CreateVersion4(), reviewId, fixture.Today, "Approved.",
                new HashSet<Uuid> { fixture.OwnerMemberId }, fixture.ApproverMemberId,
                "Approver", DateTimeOffset.UtcNow));
            return Result.Success;
        });
        Assert.Empty((await QueueAsync(fixture, fixture.ApproverUserId, "mine")).Items);
    }

    [Fact]
    public async Task ShouldQueueEachAuthorizedReviewerGivenMultipleExactRevisionAssignments()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        fixture.Permissions.Managers.Add(fixture.ReviewerMemberId);
        fixture.Permissions.Managers.Add(fixture.OwnerMemberId);
        var controlId = await CreateDraftAsync(fixture, assignReviewer: true);
        await ApplyControlAsync(fixture, controlId, control =>
        {
            Assign(control, fixture, fixture.OwnerMemberId,
                ResponsibilityType.AssignedReviewer, DateTimeOffset.UtcNow.AddMinutes(-1));
            return Result.Success;
        });

        // Act
        var reviewerQueue = await QueueAsync(fixture, fixture.ReviewerUserId, "mine");
        var ownerQueue = await QueueAsync(fixture, fixture.OwnerUserId, "mine");

        // Assert
        var reviewerItem = Assert.Single(reviewerQueue.Items);
        var ownerItem = Assert.Single(ownerQueue.Items);
        Assert.Equal("control_draft_review", reviewerItem.Kind);
        Assert.Equal("control_draft_review", ownerItem.Kind);
        Assert.NotEqual(reviewerItem.WorkItemId, ownerItem.WorkItemId);
        Assert.Equal(fixture.ReviewerMemberId, reviewerItem.AssigneeMemberId);
        Assert.Equal(fixture.OwnerMemberId, ownerItem.AssigneeMemberId);
    }

    [Fact]
    public async Task ShouldCreateNewReviewWorkIdentityGivenChangesRequested()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var controlId = await CreateDraftAsync(fixture, assignReviewer: false);
        var first = Assert.Single((await QueueAsync(fixture, fixture.ApproverUserId,
            "unassigned")).Items);
        await ApplyControlAsync(fixture, controlId, control =>
        {
            Assert.Null(control.Review(fixture.ProgramId, control.Revision,
                Uuid.CreateVersion4(), "request_changes", "Clarify the procedure.",
                fixture.ReviewerMemberId, "Reviewer", DateTimeOffset.UtcNow));
            return Result.Success;
        });

        // Act
        var afterChanges = Assert.Single((await QueueAsync(fixture, fixture.ApproverUserId,
            "unassigned")).Items);

        // Assert
        Assert.Equal("control_draft_review", first.Kind);
        Assert.Equal(first.SourceId, afterChanges.SourceId);
        Assert.NotEqual(first.WorkItemId, afterChanges.WorkItemId);
    }

    [Fact]
    public async Task ShouldFallBackToUnassignedGivenReviewerAssignmentForPriorRevision()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var controlId = await CreateDraftAsync(fixture, assignReviewer: true);
        await ApplyControlAsync(fixture, controlId, control =>
        {
            Assert.Null(control.Revise(fixture.ProgramId, control.Revision,
                ControlContent() with { Title = "Updated access review" }, fixture.LeadMemberId,
                "Lead", DateTimeOffset.UtcNow));
            return Result.Success;
        });

        // Act
        var managerQueue = await QueueAsync(fixture, fixture.ApproverUserId, "unassigned");
        var priorReviewerQueue = await QueueAsync(fixture, fixture.ReviewerUserId, "mine");

        // Assert
        var item = Assert.Single(managerQueue.Items);
        Assert.Equal("control_draft_review", item.Kind);
        Assert.Contains("revision 2", item.Reason, StringComparison.Ordinal);
        Assert.Null(item.AssigneeMemberId);
        Assert.Empty(priorReviewerQueue.Items);
    }

    [Fact]
    public async Task ShouldKeepDecisionActionableGivenAssignedMemberCannotManageProgram()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var controlId = await CreateDraftAsync(fixture, assignReviewer: true);

        // Act
        var managerQueue = await QueueAsync(fixture, fixture.ApproverUserId, "unassigned");
        var reviewerQueue = await QueueAsync(fixture, fixture.ReviewerUserId, "mine");

        // Assert
        var item = Assert.Single(managerQueue.Items);
        Assert.Equal("control_draft_review", item.Kind);
        Assert.Equal(new OperatingHolder(OperatingAuthority.ProgramReviewerHolder,
            fixture.ProgramId), item.Responsible);
        Assert.Null(item.AssigneeMemberId);
        Assert.Empty(reviewerQueue.Items);
    }

    [Fact]
    public async Task ShouldHideDraftDecisionGivenActivationReleaseGateDisabled()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await CreateDraftAsync(fixture, assignReviewer: true);

        // Act
        var queue = await ReadQueueAsync(fixture, fixture.TenantId, fixture.ProgramId,
            activationEnabled: false, lifecycleEnabled: true);

        // Assert
        Assert.True(queue.IsSuccess);
        Assert.DoesNotContain(queue.Value.Entries,
            static entry => entry.Candidate.Kind == "control_draft_review");
    }

    [Fact]
    public async Task ShouldHideRetirementDecisionGivenLifecycleReleaseGateDisabled()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await ApplyControlAsync(fixture, fixture.ControlId, control =>
        {
            Assert.Null(control.ProposeRetirement(fixture.ProgramId, fixture.ControlVersionId,
                fixture.Today.AddDays(10), "No longer applicable.", fixture.LeadMemberId,
                "Lead", DateTimeOffset.UtcNow));
            return Result.Success;
        });

        // Act
        var enabled = await ReadQueueAsync(fixture, fixture.TenantId, fixture.ProgramId,
            activationEnabled: true, lifecycleEnabled: true);
        var disabled = await ReadQueueAsync(fixture, fixture.TenantId, fixture.ProgramId,
            activationEnabled: true, lifecycleEnabled: false);

        // Assert
        var item = Assert.Single(enabled.Value.Entries,
            static entry => entry.Candidate.Kind == "control_retirement_review");
        Assert.Equal("review", item.Item.NextAction);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"controls/{fixture.ControlId}/draft/reviews", item.Item.ActionPath);

        // An accepted retirement review advances to the existing HTTP-only approval action.
        await ApplyControlAsync(fixture, fixture.ControlId, control =>
        {
            Assert.Null(control.Review(fixture.ProgramId, control.Revision,
                Uuid.CreateVersion4(), "accept", "Retirement impact reviewed.",
                fixture.ReviewerMemberId, "Reviewer", DateTimeOffset.UtcNow));
            return Result.Success;
        });
        var retirementApproval = await ReadQueueAsync(fixture, fixture.TenantId,
            fixture.ProgramId, activationEnabled: true, lifecycleEnabled: true);
        var approval = Assert.Single(retirementApproval.Value.Entries,
            static entry => entry.Candidate.Kind == "control_retirement_approval");
        Assert.Equal("approve", approval.Item.NextAction);
        Assert.EndsWith($"/controls/{fixture.ControlId}/retirements", approval.Item.ActionPath,
            StringComparison.Ordinal);

        Assert.DoesNotContain(disabled.Value.Entries,
            static entry => entry.Candidate.Kind.StartsWith("control_retirement_",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldQueueSuccessorReviewOnlyGivenEnabledControlLifecycle()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        fixture.Permissions.Managers.Add(fixture.ReviewerMemberId);
        await ApplyControlAsync(fixture, fixture.ControlId, control =>
        {
            Assert.Null(control.ProposeSuccessor(fixture.ProgramId, fixture.ControlVersionId,
                ControlContent() with { Title = "Updated access review" }, fixture.LeadMemberId,
                "Lead", DateTimeOffset.UtcNow));
            Assign(control, fixture, fixture.ReviewerMemberId,
                ResponsibilityType.AssignedReviewer, DateTimeOffset.UtcNow);
            return Result.Success;
        });

        // Act
        var enabled = await ReadQueueAsync(fixture, fixture.TenantId, fixture.ProgramId,
            activationEnabled: true, lifecycleEnabled: true);
        var disabled = await ReadQueueAsync(fixture, fixture.TenantId, fixture.ProgramId,
            activationEnabled: true, lifecycleEnabled: false);
        var reviewerQueue = await QueueAsync(fixture, fixture.ReviewerUserId, "mine");

        // Assert
        Assert.DoesNotContain(disabled.Value.Entries,
            static entry => entry.Candidate.Kind == "control_draft_review");
        var item = Assert.Single(enabled.Value.Entries,
            static entry => entry.Candidate.Kind == "control_draft_review");
        Assert.Equal(ControlVersionIds.Sequence(fixture.ControlId, 2), item.Item.SourceId);
        Assert.Equal("control_draft_review", item.Item.Kind);
        Assert.Contains("revision 2", item.Item.Reason, StringComparison.Ordinal);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"controls/{fixture.ControlId}/draft/reviews", item.Item.ActionPath);
        Assert.Single(reviewerQueue.Items);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenControlProjectionChangesDuringRead()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var controlId = await CreateDraftAsync(fixture, assignReviewer: true);
        var directory = new CheckpointAdvancingControls(fixture.TenantId,
            fixture.ProgramId, controlId);
        var consistency = new ControlDraftListReadConsistency(directory,
            new InMemoryEventStore());

        // Act
        var queue = await ReadQueueAsync(fixture, fixture.TenantId, fixture.ProgramId,
            activationEnabled: true, lifecycleEnabled: true, consistency, directory);

        // Assert
        Assert.False(queue.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, queue.Error!.Kind);
        Assert.True(queue.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldUseProjectedControlDecisionGivenCompleteProjection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        fixture.Permissions.Managers.Add(fixture.ReviewerMemberId);
        await CreateDraftAsync(fixture, assignReviewer: true);
        var sourceQueue = await ReadQueueAsync(fixture, fixture.TenantId, fixture.ProgramId,
            activationEnabled: true, lifecycleEnabled: true);
        var source = Assert.Single(sourceQueue.Value.Entries,
            static entry => entry.Candidate.Kind == "control_draft_review");
        var projection = new ProjectedControlDecisions(source.Candidate);
        var directControls = new CountingControlDraftDirectoryReader();

        // Act
        var queue = await ReadQueueAsync(fixture, fixture.TenantId, fixture.ProgramId,
            activationEnabled: true, lifecycleEnabled: true,
            controls: directControls, projectedWorkItems: [projection]);

        // Assert
        Assert.True(queue.IsSuccess);
        Assert.Single(queue.Value.Entries,
            static entry => entry.Candidate.Kind == "control_draft_review");
        Assert.Equal(0, directControls.ListProgramCalls);
    }

    [Fact]
    public async Task ShouldKeepControlDecisionWorkWithinTenantAndProgramGivenDifferentScope()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var controlId = await CreateDraftAsync(fixture, assignReviewer: true);

        // Act
        var otherProgram = await ReadQueueAsync(fixture, fixture.TenantId,
            Uuid.CreateVersion4(), activationEnabled: true, lifecycleEnabled: true);
        var otherTenant = await ReadQueueAsync(fixture, Uuid.CreateVersion4(), fixture.ProgramId,
            activationEnabled: true, lifecycleEnabled: true);

        // Assert
        Assert.True(otherProgram.IsSuccess);
        Assert.True(otherTenant.IsSuccess);
        Assert.DoesNotContain(otherProgram.Value.Entries,
            entry => entry.Candidate.SourceId == ControlVersionIds.Initial(controlId));
        Assert.DoesNotContain(otherTenant.Value.Entries,
            entry => entry.Candidate.SourceId == ControlVersionIds.Initial(controlId));
    }

    static async Task<Result<WorkQueueSnapshot>> ReadQueueAsync(OperationsFixture fixture,
        Uuid tenantId, Uuid programId, bool activationEnabled, bool lifecycleEnabled,
        ControlDraftListReadConsistency? consistency = null,
        IControlDraftDirectoryReader? controls = null,
        IEnumerable<IAccountableWorkItemDirectoryReader>? projectedWorkItems = null)
    {
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System,
            services.GetRequiredService<WorkQueueReadConsistency>(),
            controls: controls ?? services.GetRequiredService<IControlDraftDirectoryReader>(),
            controlConsistency: consistency,
            controlActivationGate: new ControlActivationReleaseGate(activationEnabled),
            controlLifecycleGate: new ControlLifecycleReleaseGate(lifecycleEnabled),
            accountableWorkItems: projectedWorkItems);
        var actor = new OperationsActor(fixture.ApproverUserId, fixture.ApproverMemberId,
            "Approver");
        return await queue.ReadAsync(tenantId, programId, actor, 30, CancellationToken.None);
    }

    static Task<WorkQueueView> QueueAsync(OperationsFixture fixture, Uuid userId,
        string scope) => fixture.AsAsync(userId,
        new ListWork(fixture.TenantId, fixture.ProgramId, scope));

    static async Task<Uuid> CreateDraftAsync(OperationsFixture fixture,
        bool assignReviewer, bool assignApprover = false, bool assignOwner = false)
    {
        var controlId = Uuid.CreateVersion4();
        var changedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        await ApplyControlAsync(fixture, controlId, control =>
        {
            Assert.True(control.Create(fixture.ProgramId, Uuid.CreateVersion4(), "AC-WORK",
                ControlContent(), fixture.LeadMemberId, "Lead", changedAt)
                .IsSuccess);
            if (assignReviewer)
                Assign(control, fixture, fixture.ReviewerMemberId,
                    ResponsibilityType.AssignedReviewer, changedAt);
            if (assignApprover)
                Assign(control, fixture, fixture.ApproverMemberId,
                    ResponsibilityType.PolicyApprover, changedAt);
            if (assignOwner)
                Assign(control, fixture, fixture.OwnerMemberId,
                    ResponsibilityType.ControlOwner, changedAt);
            return Result.Success;
        });
        fixture.IncludeControlInDirectory(controlId);
        return controlId;
    }

    static void Assign(ControlDraft control, OperationsFixture fixture, Uuid memberId,
        ResponsibilityType type, DateTimeOffset at)
    {
        Assert.Null(control.AssignResponsibility(new ResponsibilityScope("control", control.Id,
                control.DraftVersionId, control.Revision), Uuid.CreateVersion4(), memberId,
            type, fixture.LeadMemberId, "Lead", at, at.AddMinutes(-1), null, []));
    }

    static ControlDraftContent ControlContent() => new("Access review", "Review access",
        "Management reviews user access.", "Review access monthly.", ["Signed review record"]);

    static Task ApplyControlAsync(OperationsFixture fixture, Uuid controlId,
        Func<ControlDraft, Result> apply) => ProgramManagementServices.SeedAsync(
        fixture.Provider, new ControlDraft(fixture.TenantId, controlId), apply);

    sealed class CheckpointAdvancingControls(Uuid tenantId, Uuid programId, Uuid controlId)
        : IControlDraftDirectoryReader
    {
        int _checkpointReads;
        ProjectionCheckpoint _checkpoint = ProjectionCheckpoint.Start;

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid requestedTenantId,
            CancellationToken ct = default)
        {
            if (requestedTenantId == tenantId && ++_checkpointReads == 2)
                _checkpoint = new ProjectionCheckpoint(new EventCursor("advanced"));
            return ValueTask.FromResult(_checkpoint);
        }

        public ValueTask<ControlDraftView?> GetAsync(Uuid requestedTenantId, Uuid requestedControlId,
            CancellationToken ct = default) => ValueTask.FromResult<ControlDraftView?>(null);

        public ValueTask<Page<ControlDraftView>> ListProgramAsync(Uuid requestedTenantId,
            Uuid requestedProgramId, int limit, string? cursor, CancellationToken ct = default)
        {
            IReadOnlyList<ControlDraftView> items = requestedTenantId == tenantId &&
                requestedProgramId == programId
                ? [new ControlDraftView(tenantId, programId, controlId, "AC-WORK", 1,
                    "draft", "unresolved", "unresolved",
                    new ControlDraftContent("Title", "Objective", "Description", "Narrative",
                        ["Expected evidence"]), Uuid.Empty, "Author", DateTimeOffset.UtcNow)]
                : [];
            return ValueTask.FromResult(new Page<ControlDraftView>(items, null));
        }

        public ValueTask<ControlDraftRevisionView?> GetRevisionAsync(Uuid requestedTenantId,
            Uuid requestedControlId, long revision, CancellationToken ct = default) =>
            ValueTask.FromResult<ControlDraftRevisionView?>(null);
    }

    sealed class ProjectedControlDecisions(WorkCandidate candidate)
        : IAccountableWorkItemDirectoryReader
    {
        static readonly string[] Kinds =
        [
            "control_draft_review",
            "control_draft_approval",
            "control_retirement_review",
            "control_retirement_approval",
        ];

        public string ProjectorName => "AccountableWorkItemControlDecisionV1";
        public IReadOnlyCollection<string> ProjectedKinds => Kinds;
        public EventStreamPattern SourcePattern(Uuid tenantId) =>
            EventStreamPattern.ForPattern(tenantId.ToString(), "controls");
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) =>
            ValueTask.FromResult(ProjectionCheckpoint.Start);
        public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
            Uuid programId, CancellationToken ct = default) =>
            ValueTask.FromResult(Result<IReadOnlyList<WorkCandidate>>.Success([candidate]));
    }

    sealed class CountingControlDraftDirectoryReader : IControlDraftDirectoryReader
    {
        public int ListProgramCalls { get; private set; }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) =>
            ValueTask.FromResult(ProjectionCheckpoint.Start);

        public ValueTask<ControlDraftView?> GetAsync(Uuid tenantId, Uuid controlId,
            CancellationToken ct = default) => ValueTask.FromResult<ControlDraftView?>(null);

        public ValueTask<Page<ControlDraftView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default)
        {
            ListProgramCalls++;
            return ValueTask.FromResult(new Page<ControlDraftView>([], null));
        }

        public ValueTask<ControlDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid controlId,
            long revision, CancellationToken ct = default) =>
            ValueTask.FromResult<ControlDraftRevisionView?>(null);
    }
}
