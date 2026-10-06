using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WorkQueueTests
{
    [Fact]
    public async Task ShouldPlaceUndatedReviewLastGivenEvidenceDueOnLatestSupportedDate()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var boundaryId = Uuid.CreateVersion4();
        var draftVersionId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var content = new BoundaryContent("SOC 2 system boundary", "readiness", ["security"], []);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.True(boundary.Create(fixture.ProgramId, draftVersionId, content,
                    fixture.LeadMemberId, "Lead", now.AddMinutes(-5)).IsSuccess);
                Assert.Null(boundary.AssignResponsibility(new ResponsibilityScope("boundary",
                        boundaryId, draftVersionId, 1), Uuid.CreateVersion4(),
                    fixture.ReviewerMemberId, ResponsibilityType.AssignedReviewer,
                    fixture.LeadMemberId, "Lead", now.AddMinutes(-4), now.AddMinutes(-4), null, []));
                return Result.Success;
            });
        fixture.Boundaries.Add(new BoundaryView(fixture.TenantId, boundaryId, fixture.ProgramId,
            new BoundaryVersionView(fixture.TenantId, boundaryId, fixture.ProgramId,
                draftVersionId, 1, content, "draft", null, fixture.LeadMemberId, "Lead",
                now.AddMinutes(-5)), null, null, 1));
        var evidence = await fixture.AsAsync(fixture.LeadUserId,
            new OpenEvidenceRequest(fixture.TenantId, fixture.ProgramId, "Future evidence",
                "Keep dated work before undated work.", fixture.ReviewerMemberId,
                DateOnly.MaxValue));

        // Act
        var queue = await fixture.AsAsync(fixture.ReviewerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));

        // Assert
        Assert.Equal(2, queue.Items.Count);
        Assert.Equal(new WorkCountsView(2, 0, 0, 0), queue.Counts);
        Assert.Equal("evidence_request", queue.Items[0].Kind);
        Assert.Equal(evidence.EvidenceRequestId, queue.Items[0].SourceId);
        Assert.Equal(DateOnly.MaxValue, queue.Items[0].DueOn);
        Assert.Equal("boundary_review", queue.Items[1].Kind);
        Assert.Equal(draftVersionId, queue.Items[1].SourceId);
        Assert.Null(queue.Items[1].DueOn);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(7)]
    public async Task ShouldReconcileQueueAndRemindersGivenLatestSupportedDueDates(
        int daysBeforeMaximum)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId,
            fixture.Today.AddDays(-7), fixture.Today.AddDays(-6), fixture.Today);
        var dueOn = DateOnly.MaxValue.AddDays(-daysBeforeMaximum);
        var evidence = await fixture.AsAsync(fixture.LeadUserId,
            new OpenEvidenceRequest(fixture.TenantId, fixture.ProgramId, "Future evidence",
                "Keep its source action available.", fixture.OwnerMemberId, dueOn,
                fixture.ControlId));

        // Act
        var queue = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));
        var reminders = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWorkReminders(fixture.TenantId, fixture.ProgramId));
        var digest = await fixture.AsAsync(fixture.OwnerUserId,
            new GetWorkDigest(fixture.TenantId, fixture.ProgramId));

        // Assert
        Assert.Equal(new WorkCountsView(4, 2, 1, 1), queue.Counts);
        Assert.Equal(4, queue.Items.Count);
        var item = Assert.Single(queue.Items, item => item.Kind == "evidence_request");
        Assert.Equal(evidence.EvidenceRequestId, item.SourceId);
        Assert.Equal(dueOn, item.DueOn);
        Assert.False(item.Overdue);
        Assert.False(item.Escalated);
        Assert.Null(item.EscalatedBy);
        Assert.Equal("fulfil", item.NextAction);
        Assert.EndsWith($"/evidence-requests/{evidence.EvidenceRequestId}/fulfilments",
            item.ActionPath, StringComparison.Ordinal);
        var detail = await fixture.AsAsync(fixture.OwnerUserId,
            new GetWorkItem(fixture.TenantId, fixture.ProgramId, item.WorkItemId));
        Assert.Equal(item, detail.Item);
        Assert.Equal(3, reminders.Count);
        Assert.DoesNotContain(reminders, reminder => reminder.WorkItemId == item.WorkItemId);
        Assert.Equal(2, digest.Overdue.Count);
        Assert.Equal(fixture.Today, Assert.Single(digest.DueSoon).DueOn);
        Assert.DoesNotContain(digest.Overdue.Concat(digest.DueSoon),
            entry => entry.WorkItemId == item.WorkItemId);
    }

    [Fact]
    public async Task ShouldListOpenEvidenceRequestForItsOwnerAndDropItGivenCancellation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var request = await fixture.AsAsync(fixture.LeadUserId, new OpenEvidenceRequest(fixture.TenantId,
            fixture.ProgramId, "Q3 access review export", "Upload the signed record.", fixture.OwnerMemberId,
            fixture.Today.AddDays(5), fixture.ControlId));

        // Act
        var before = await fixture.AsAsync(fixture.OwnerUserId, new ListWork(fixture.TenantId, fixture.ProgramId));
        await fixture.AsAsync(fixture.LeadUserId, new CancelEvidenceRequest(fixture.TenantId, fixture.ProgramId,
            request.EvidenceRequestId, request.Revision, "Not needed"));
        var after = await fixture.AsAsync(fixture.OwnerUserId, new ListWork(fixture.TenantId, fixture.ProgramId));

        // Assert
        var item = Assert.Single(before.Items, item => item.Kind == "evidence_request");
        Assert.Equal(request.EvidenceRequestId, item.SourceId);
        Assert.Equal(fixture.ControlId, item.ControlId);
        Assert.Equal("fulfil", item.NextAction);
        Assert.Equal(fixture.Today.AddDays(5), item.DueOn);
        Assert.Equal(fixture.OwnerMemberId, item.AssigneeMemberId);
        Assert.EndsWith($"/evidence-requests/{request.EvidenceRequestId}/fulfilments", item.ActionPath,
            StringComparison.Ordinal);
        Assert.DoesNotContain(after.Items, item => item.Kind == "evidence_request");
    }

    [Fact]
    public async Task ShouldListAssignedBoundaryReviewForReviewerGivenCurrentUnreviewedDraft()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var boundaryId = Uuid.CreateVersion4();
        var draftVersionId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var content = new BoundaryContent("SOC 2 system boundary", "readiness", ["security"], []);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.True(boundary.Create(fixture.ProgramId, draftVersionId, content,
                    fixture.LeadMemberId, "Lead", now.AddMinutes(-5)).IsSuccess);
                Assert.Null(boundary.AssignResponsibility(new ResponsibilityScope("boundary",
                        boundaryId, draftVersionId, 1), Uuid.CreateVersion4(),
                    fixture.ReviewerMemberId, ResponsibilityType.AssignedReviewer,
                    fixture.LeadMemberId, "Lead", now.AddMinutes(-4), now.AddMinutes(-4), null, []));
                Assert.Null(boundary.AssignResponsibility(new ResponsibilityScope("boundary",
                        boundaryId, draftVersionId, 1), Uuid.CreateVersion4(),
                    fixture.BackupMemberId, ResponsibilityType.AssignedReviewer,
                    fixture.LeadMemberId, "Lead", now.AddMinutes(-4), now.AddMinutes(-4), null, []));
                Assert.Null(boundary.AssignResponsibility(new ResponsibilityScope("boundary",
                        boundaryId, draftVersionId, 1), Uuid.CreateVersion4(),
                    fixture.LeadMemberId, ResponsibilityType.AssignedReviewer,
                    fixture.ApproverMemberId, "Approver", now.AddMinutes(-4), now.AddMinutes(-4), null, []));
                return Result.Success;
            });
        fixture.Boundaries.Add(new BoundaryView(fixture.TenantId, boundaryId, fixture.ProgramId,
            new BoundaryVersionView(fixture.TenantId, boundaryId, fixture.ProgramId,
                draftVersionId, 1, content, "draft", null, fixture.LeadMemberId, "Lead",
                now.AddMinutes(-5)), null, null, 1));

        // Act
        var queue = await fixture.AsAsync(fixture.ReviewerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));

        // Assert
        var item = Assert.Single(queue.Items);
        Assert.Equal("boundary_review", item.Kind);
        Assert.Equal(draftVersionId, item.SourceId);
        Assert.Equal("review", item.NextAction);
        Assert.Equal(fixture.ReviewerMemberId, item.AssigneeMemberId);
        Assert.EndsWith($"/boundaries/{boundaryId}/drafts/{draftVersionId}/reviews",
            item.ActionPath, StringComparison.Ordinal);
        var backupQueue = await fixture.AsAsync(fixture.BackupUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));
        Assert.NotEqual(item.WorkItemId, Assert.Single(backupQueue.Items).WorkItemId);
        var authorQueue = await fixture.AsAsync(fixture.LeadUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));
        Assert.Empty(authorQueue.Items);
    }

    [Fact]
    public async Task ShouldRouteAcceptedBoundaryDraftToAssignedApproverGivenAcceptedReviewUntilApproval()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var boundaryId = Uuid.CreateVersion4();
        var draftVersionId = Uuid.CreateVersion4();
        var reviewDecisionId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var content = new BoundaryContent("SOC 2 system boundary", "readiness", ["security"], []);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.True(boundary.Create(fixture.ProgramId, draftVersionId, content,
                    fixture.LeadMemberId, "Lead", now.AddMinutes(-10)).IsSuccess);
                Assert.Null(boundary.AssignResponsibility(new ResponsibilityScope("boundary",
                        boundaryId, draftVersionId, 1), Uuid.CreateVersion4(),
                    fixture.ReviewerMemberId, ResponsibilityType.AssignedReviewer,
                    fixture.LeadMemberId, "Lead", now.AddMinutes(-9), now.AddMinutes(-9), null, []));
                Assert.Null(boundary.AssignResponsibility(new ResponsibilityScope("boundary",
                        boundaryId, draftVersionId, 1), Uuid.CreateVersion4(),
                    fixture.ApproverMemberId, ResponsibilityType.PolicyApprover,
                    fixture.LeadMemberId, "Lead", now.AddMinutes(-8), now.AddMinutes(-8), null, []));
                return Result.Success;
            });
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.Null(boundary.Review(draftVersionId, 1, reviewDecisionId, "accept",
                    "Reviewed independently.", fixture.ReviewerMemberId, "Reviewer",
                    now.AddMinutes(-5)));
                return Result.Success;
            });
        var draft = new BoundaryVersionView(fixture.TenantId, boundaryId, fixture.ProgramId,
            draftVersionId, 1, content, "draft", null, fixture.LeadMemberId, "Lead",
            now.AddMinutes(-10));
        var review = new BoundaryDecisionView(fixture.TenantId, boundaryId, reviewDecisionId,
            draftVersionId, 1, "accept", fixture.ReviewerMemberId, "Reviewer",
            "Reviewed independently.", now.AddMinutes(-5), null, null, null);
        fixture.Boundaries.Add(new BoundaryView(fixture.TenantId, boundaryId, fixture.ProgramId,
            draft, null, review, 2));

        // Act
        var reviewerQueue = await fixture.AsAsync(fixture.ReviewerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));
        var approverQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));

        // Assert
        Assert.Empty(reviewerQueue.Items);
        var item = Assert.Single(approverQueue.Items);
        Assert.Equal("boundary_approval", item.Kind);
        Assert.Equal(draftVersionId, item.SourceId);
        Assert.Equal("approve", item.NextAction);
        Assert.Equal(fixture.ApproverMemberId, item.AssigneeMemberId);
        Assert.EndsWith($"/boundaries/{boundaryId}/drafts/{draftVersionId}/approvals",
            item.ActionPath, StringComparison.Ordinal);

        // Act
        var approvalDecisionId = Uuid.CreateVersion4();
        var approvedEffectiveFrom = DateOnly.FromDateTime(now.UtcDateTime).AddDays(1);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.Null(boundary.Approve(draftVersionId, 1, approvalDecisionId,
                    reviewDecisionId, approvedEffectiveFrom, "Approved.", "impact-digest",
                    fixture.ApproverMemberId, "Approver", now.AddMinutes(-2)));
                return Result.Success;
            });
        fixture.Boundaries.Add(new BoundaryView(fixture.TenantId, boundaryId, fixture.ProgramId,
            null, draft with { Status = "approved", EffectiveFrom = approvedEffectiveFrom },
            review with { DecisionId = approvalDecisionId, Outcome = "approve" }, 3));
        var completedQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));

        // Assert
        Assert.Empty(completedQueue.Items);
    }

    [Fact]
    public async Task ShouldNotDuplicateBoundaryReviewGivenProjectionAndLiveSource()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var boundaryId = Uuid.CreateVersion4();
        var draftVersionId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var changedAt = now.AddMinutes(-5);
        var content = new BoundaryContent("SOC 2 system boundary", "readiness", ["security"], []);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.True(boundary.Create(fixture.ProgramId, draftVersionId, content,
                    fixture.LeadMemberId, "Lead", changedAt).IsSuccess);
                Assert.Null(boundary.AssignResponsibility(new ResponsibilityScope("boundary",
                        boundaryId, draftVersionId, 1), Uuid.CreateVersion4(),
                    fixture.ReviewerMemberId, ResponsibilityType.AssignedReviewer,
                    fixture.LeadMemberId, "Lead", changedAt.AddMinutes(1), changedAt.AddMinutes(1),
                    null, []));
                return Result.Success;
            });
        fixture.Boundaries.Add(new BoundaryView(fixture.TenantId, boundaryId, fixture.ProgramId,
            new BoundaryVersionView(fixture.TenantId, boundaryId, fixture.ProgramId,
                draftVersionId, 1, content, "draft", null, fixture.LeadMemberId, "Lead",
                changedAt), null, null, 1));
        await fixture.CatchUpBoundaryDirectoryAsync();

        await using var scope = fixture.Provider.CreateAsyncScope();
        var sourceReader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var live = await BoundaryDecisionWork.LoadAsync(sourceReader, fixture.Boundaries,
            fixture.TenantId, fixture.ProgramId, now, null, CancellationToken.None);
        var candidate = Assert.Single(live.Value);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var projected = new ProjectedBoundaryWorkItemDirectory(candidate,
            await ReadBoundaryCheckpointAsync(events, fixture.TenantId));
        var consistency = new WorkQueueReadConsistency(events, [projected]);
        var queue = new WorkQueueReader(sourceReader,
            scope.ServiceProvider.GetRequiredService<OperatingAuthority>(), TimeProvider.System,
            consistency, boundaries: fixture.Boundaries, accountableWorkItems: [projected]);
        var actor = new OperationsActor(fixture.ReviewerUserId, fixture.ReviewerMemberId,
            "Reviewer");

        // Act
        var result = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId, actor, 30,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Entries, entry => entry.Item.Kind == "boundary_review");
    }

    [Fact]
    public async Task ShouldOmitBoundaryReviewGivenExpiredResponsibility()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var boundaryId = Uuid.CreateVersion4();
        var draftVersionId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var content = new BoundaryContent("SOC 2 system boundary", "readiness", ["security"], []);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.True(boundary.Create(fixture.ProgramId, draftVersionId, content,
                    fixture.LeadMemberId, "Lead", now.AddMinutes(-5)).IsSuccess);
                Assert.Null(boundary.AssignResponsibility(new ResponsibilityScope("boundary",
                        boundaryId, draftVersionId, 1), Uuid.CreateVersion4(),
                    fixture.ReviewerMemberId, ResponsibilityType.AssignedReviewer,
                    fixture.LeadMemberId, "Lead", now.AddMinutes(-4), now.AddMinutes(-4),
                    now.AddMinutes(-1), []));
                return Result.Success;
            });
        fixture.Boundaries.Add(new BoundaryView(fixture.TenantId, boundaryId, fixture.ProgramId,
            new BoundaryVersionView(fixture.TenantId, boundaryId, fixture.ProgramId,
                draftVersionId, 1, content, "draft", null, fixture.LeadMemberId, "Lead",
                now.AddMinutes(-5)), null, null, 1));

        // Act
        var queue = await fixture.AsAsync(fixture.ReviewerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));

        // Assert
        Assert.Empty(queue.Items);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenStaleBoundaryDecisionProjection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var boundaryId = Uuid.CreateVersion4();
        var draftVersionId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var content = new BoundaryContent("SOC 2 system boundary", "readiness", ["security"], []);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.True(boundary.Create(fixture.ProgramId, draftVersionId, content,
                    fixture.LeadMemberId, "Lead", now.AddMinutes(-5)).IsSuccess);
                Assert.Null(boundary.AssignResponsibility(new ResponsibilityScope("boundary",
                        boundaryId, draftVersionId, 1), Uuid.CreateVersion4(),
                    fixture.ReviewerMemberId, ResponsibilityType.AssignedReviewer,
                    fixture.LeadMemberId, "Lead", now.AddMinutes(-4), now.AddMinutes(-4), null, []));
                return Result.Success;
            });
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.Null(boundary.Review(draftVersionId, 1, Uuid.CreateVersion4(), "accept",
                    "Reviewed independently.", fixture.ReviewerMemberId, "Reviewer",
                    now.AddMinutes(-2)));
                return Result.Success;
            });
        fixture.Boundaries.Add(new BoundaryView(fixture.TenantId, boundaryId, fixture.ProgramId,
            new BoundaryVersionView(fixture.TenantId, boundaryId, fixture.ProgramId,
                draftVersionId, 1, content, "draft", null, fixture.LeadMemberId, "Lead",
                now.AddMinutes(-5)), null, null, 1));

        // Act
        var stale = await fixture.Scenario(fixture.ReviewerUserId)
            .When(new ListWork(fixture.TenantId, fixture.ProgramId))
            .ExpectFailure(RequestErrorKind.Conflict);

        // Assert
        Assert.True(stale.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenBoundaryProjectionAheadOfSource()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var boundaryId = Uuid.CreateVersion4();
        var draftVersionId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var content = new BoundaryContent("SOC 2 system boundary", "readiness", ["security"], []);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.True(boundary.Create(fixture.ProgramId, draftVersionId, content,
                    fixture.LeadMemberId, "Lead", now.AddMinutes(-5)).IsSuccess);
                return Result.Success;
            });
        fixture.Boundaries.Add(new BoundaryView(fixture.TenantId, boundaryId, fixture.ProgramId,
            new BoundaryVersionView(fixture.TenantId, boundaryId, fixture.ProgramId,
                draftVersionId, 1, content, "draft", null, fixture.LeadMemberId, "Lead",
                now.AddMinutes(-5)), null, null, 2));

        // Act
        var stale = await fixture.Scenario(fixture.ReviewerUserId)
            .When(new ListWork(fixture.TenantId, fixture.ProgramId))
            .ExpectFailure(RequestErrorKind.Conflict);

        // Assert
        Assert.True(stale.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenPendingBoundaryDraftMissingFromDirectory()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var boundaryId = Uuid.CreateVersion4();
        var draftVersionId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var content = new BoundaryContent("SOC 2 system boundary", "readiness", ["security"], []);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.True(boundary.Create(fixture.ProgramId, draftVersionId, content,
                    fixture.LeadMemberId, "Lead", now.AddMinutes(-5)).IsSuccess);
                Assert.Null(boundary.AssignResponsibility(new ResponsibilityScope("boundary",
                        boundaryId, draftVersionId, 1), Uuid.CreateVersion4(),
                    fixture.ReviewerMemberId, ResponsibilityType.AssignedReviewer,
                    fixture.LeadMemberId, "Lead", now.AddMinutes(-4), now.AddMinutes(-4), null, []));
                return Result.Success;
            });

        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var projection = new ProjectedBoundaryWorkItemDirectory(null, ProjectionCheckpoint.Start);
        var consistency = new WorkQueueReadConsistency(events, [projection]);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            accountableWorkItems: [projection]);

        // Act
        var stale = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId,
            new OperationsActor(fixture.ReviewerUserId, fixture.ReviewerMemberId, "Reviewer"),
            30, CancellationToken.None);

        // Assert
        Assert.False(stale.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error.Kind);
        Assert.True(stale.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenBoundarySourceChangesDuringQueueRead()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var boundaryId = Uuid.CreateVersion4();
        var draftVersionId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var content = new BoundaryContent("SOC 2 system boundary", "readiness", ["security"], []);
        var projection = new ProjectedBoundaryWorkItemDirectory(null, ProjectionCheckpoint.Start,
            async _ =>
        {
            await ProgramManagementServices.SeedAsync(fixture.Provider,
                new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
                {
                    Assert.True(boundary.Create(fixture.ProgramId, draftVersionId, content,
                        fixture.LeadMemberId, "Lead", now.AddMinutes(-5)).IsSuccess);
                    return Result.Success;
                });
        });
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var consistency = new WorkQueueReadConsistency(events, [projection]);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            accountableWorkItems: [projection]);

        // Act
        var stale = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId,
            new OperationsActor(fixture.ReviewerUserId, fixture.ReviewerMemberId, "Reviewer"),
            30, CancellationToken.None);

        // Assert
        Assert.False(stale.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error.Kind);
        Assert.True(stale.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenRequiredSourceChangesDuringQueueRead()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var evidenceId = Uuid.CreateVersion4();
        var lead = ActorReference.ForMember(fixture.LeadMemberId, "Lead");
        fixture.Policies.OnList = async _ =>
        {
            fixture.Policies.OnList = null;
            await ProgramManagementServices.SeedAsync(fixture.Provider,
                new EvidenceRequestLedger(fixture.TenantId, fixture.ProgramId), ledger =>
                {
                    Assert.Null(ledger.OpenRequest(evidenceId, "Access export", "Upload the export.",
                        fixture.OwnerMemberId, fixture.Today.AddDays(5), null, lead,
                        DateTimeOffset.UtcNow));
                    return Result.Success;
                });
        };
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var evidence = new ProjectedEvidenceWorkItemDirectory(ProjectionCheckpoint.Start);
        var consistency = new WorkQueueReadConsistency(events, [evidence]);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            policies: fixture.Policies, accountableWorkItems: [evidence]);

        // Act
        var stale = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId,
            new OperationsActor(fixture.OwnerUserId, fixture.OwnerMemberId, "Owner"), 30,
            CancellationToken.None);

        // Assert
        Assert.False(stale.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error.Kind);
        Assert.True(stale.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldOmitBoundaryApprovalGivenForeignProjectedDecision()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var boundaryId = Uuid.CreateVersion4();
        var draftVersionId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var content = new BoundaryContent("SOC 2 system boundary", "readiness", ["security"], []);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.True(boundary.Create(fixture.ProgramId, draftVersionId, content,
                    fixture.LeadMemberId, "Lead", now.AddMinutes(-5)).IsSuccess);
                Assert.Null(boundary.AssignResponsibility(new ResponsibilityScope("boundary",
                        boundaryId, draftVersionId, 1), Uuid.CreateVersion4(),
                    fixture.ApproverMemberId, ResponsibilityType.PolicyApprover,
                    fixture.LeadMemberId, "Lead", now.AddMinutes(-4), now.AddMinutes(-4), null, []));
                return Result.Success;
            });
        var draft = new BoundaryVersionView(fixture.TenantId, boundaryId, fixture.ProgramId,
            draftVersionId, 1, content, "draft", null, fixture.LeadMemberId, "Lead",
            now.AddMinutes(-5));
        var foreignDecision = new BoundaryDecisionView(fixture.TenantId,
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), draftVersionId, 1, "accept",
            fixture.ReviewerMemberId, "Reviewer", "Reviewed independently.",
            now.AddMinutes(-2), null, null, null);
        fixture.Boundaries.Add(new BoundaryView(fixture.TenantId, boundaryId, fixture.ProgramId,
            draft, null, foreignDecision, 1));

        // Act
        var queue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));

        // Assert
        Assert.Empty(queue.Items);
    }

    [Fact]
    public async Task ShouldListRiskTreatmentActionForAccountableMemberGivenOpenActionUntilCompletionIsSubmitted()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var riskId = Uuid.CreateVersion4();
        var actionId = Uuid.CreateVersion4();
        var evidenceId = Uuid.CreateVersion4();
        var lead = ActorReference.ForMember(fixture.LeadMemberId, "Lead");
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new EvidenceRequestLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.OpenRequest(evidenceId, "Export", "Upload.",
                    fixture.OwnerMemberId, fixture.Today.AddDays(5), null, lead,
                    DateTimeOffset.UtcNow));
                return Result.Success;
            });
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new RiskGovernanceLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.AddTreatmentAction(riskId, 0, actionId, "mitigate",
                    "Enforce MFA", "MFA everywhere.", "Policy export.", fixture.Today.AddDays(9),
                    fixture.OwnerMemberId, [evidenceId], lead, DateTimeOffset.UtcNow));
                return Result.Success;
            });

        // Act
        var before = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new EvidenceRequestLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.Fulfil(evidenceId, 1, Uuid.CreateVersion4(), lead,
                    DateTimeOffset.UtcNow));
                return Result.Success;
            });
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new RiskGovernanceLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.SubmitActionCompletion(riskId, actionId, 1,
                    Uuid.CreateVersion4(), "Done.", [evidenceId],
                    new HashSet<Uuid> { evidenceId }, fixture.OwnerMemberId, lead,
                    DateTimeOffset.UtcNow));
                return Result.Success;
            });
        var after = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));

        // Assert
        var item = Assert.Single(before.Items, item => item.Kind == "risk_treatment_action");
        Assert.Equal(actionId, item.SourceId);
        Assert.Equal("complete", item.NextAction);
        Assert.Equal(fixture.Today.AddDays(9), item.DueOn);
        Assert.Equal(fixture.OwnerMemberId, item.AssigneeMemberId);
        Assert.EndsWith($"/risks/{riskId}/treatment-actions/{actionId}/completions",
            item.ActionPath, StringComparison.Ordinal);
        Assert.DoesNotContain(after.Items, item => item.Kind == "risk_treatment_action");
    }

    [Fact]
    public async Task ShouldListSourceLinkedItemsInDeterministicOrderGivenMixedSources()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var finding = await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId,
            fixture.Today.AddDays(3));

        // Act
        var queue = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));

        // Assert
        Assert.Equal("mine", queue.Scope);
        Assert.Contains(queue.Items, item => item.Kind == "control_occurrence" && item.Overdue);
        var action = Assert.Single(queue.Items, item => item.Kind == "corrective_action");
        Assert.Equal(finding.CorrectiveActions[0].ActionId, action.SourceId);
        Assert.Equal(finding.FindingId, action.FindingId);
        Assert.Equal("complete", action.NextAction);
        Assert.EndsWith($"/corrective-actions/{action.SourceId}/completions", action.ActionPath,
            StringComparison.Ordinal);
        Assert.Equal("high", action.Materiality);
        Assert.Equal(fixture.OwnerMemberId, action.AssigneeMemberId);
        Assert.Equal(queue.Items.OrderBy(item => item.DueOn ?? DateOnly.MaxValue)
            .Select(item => item.WorkItemId), queue.Items.Select(item => item.WorkItemId));
        Assert.Equal(queue.Items.Count, queue.Counts.Total);
        Assert.Equal(queue.Items.Count(item => item.Overdue), queue.Counts.Overdue);
    }

    [Fact]
    public async Task ShouldRemoveItemGivenSourceWorkCompleted()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId,
            fixture.Today.AddDays(3));
        var item = Assert.Single((await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId))).Items);

        // Act
        await fixture.AsAsync(fixture.OwnerUserId, new CompleteCorrectiveAction(fixture.TenantId,
            fixture.ProgramId, finding.FindingId, finding.Revision,
            finding.CorrectiveActions[0].ActionId, "Removed.",
            [new EvidenceReference(null, "record", "VPN-EXPORT")]));

        // Assert
        var after = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));
        Assert.Empty(after.Items);
        Assert.Equal(0, after.Counts.Total);
        await fixture.Scenario(fixture.OwnerUserId)
            .When(new GetWorkItem(fixture.TenantId, fixture.ProgramId, item.WorkItemId))
            .ExpectFailure(RequestErrorKind.NotFound);
    }

    [Fact]
    public async Task ShouldOmitRestrictedWorkGivenUnrelatedMemberOrTenant()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId, fixture.Today);
        var item = Assert.Single((await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId))).Items);

        // Act
        var outsider = await fixture.AsAsync(fixture.OutsiderUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "all"));
        var otherTenant = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(Uuid.CreateVersion4(), fixture.ProgramId, "all"));

        // Assert
        Assert.Empty(outsider.Items);
        Assert.Equal(0, outsider.Counts.Total);
        Assert.Empty(otherTenant.Items);
        await fixture.Scenario(fixture.OutsiderUserId)
            .When(new GetWorkItem(fixture.TenantId, fixture.ProgramId, item.WorkItemId))
            .ExpectFailure(RequestErrorKind.NotFound);
        await fixture.Scenario(fixture.OwnerUserId)
            .When(new GetWorkItem(Uuid.CreateVersion4(), fixture.ProgramId, item.WorkItemId))
            .ExpectFailure(RequestErrorKind.NotFound);
    }

    [Fact]
    public async Task ShouldRejectScopeGivenUnknownScope()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();

        await fixture.Scenario(fixture.OwnerUserId)
            // Act
            .When(new ListWork(fixture.TenantId, fixture.ProgramId, "everything"))
            // Assert
            .ExpectFailure(RequestErrorKind.Validation);
    }

    [Fact]
    public async Task ShouldListReviewForReviewerOnlyGivenSubmittedAttestation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var missed = (await fixture.OccurrencesAsync("missed"))[0];

        // Act
        await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(missed));

        // Assert
        var reviewer = await fixture.AsAsync(fixture.ReviewerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));
        var review = Assert.Single(reviewer.Items);
        Assert.Equal("occurrence_review", review.Kind);
        Assert.Equal(missed.OccurrenceId, review.SourceId);
        var owner = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "all"));
        Assert.DoesNotContain(owner.Items, item => item.WorkItemId == review.WorkItemId);
    }

    static async Task<ProjectionCheckpoint> ReadBoundaryCheckpointAsync(
        IDomainEventReader events, Uuid tenantId)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(
                           tenantId.ToString(), "boundaries"), cursor, CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    sealed class ProjectedBoundaryWorkItemDirectory(WorkCandidate? candidate,
        ProjectionCheckpoint checkpoint, Func<CancellationToken, ValueTask>? onLoad = null)
        : IAccountableWorkItemDirectoryReader
    {
        static readonly string[] Kinds = ["boundary_review", "boundary_approval"];

        public string ProjectorName => "AccountableWorkItemBoundaryDecisionV1";

        public IReadOnlyCollection<string> ProjectedKinds => Kinds;

        public EventStreamPattern SourcePattern(Uuid tenantId) =>
            EventStreamPattern.ForPattern(tenantId.ToString(), "boundaries");

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);

        public async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
            Uuid programId, CancellationToken ct = default)
        {
            if (onLoad is not null)
                await onLoad(ct).ConfigureAwait(false);
            return Result<IReadOnlyList<WorkCandidate>>.Success(candidate is { } item ? [item] : []);
        }
    }

    sealed class ProjectedEvidenceWorkItemDirectory(ProjectionCheckpoint checkpoint)
        : IAccountableWorkItemDirectoryReader
    {
        public string ProjectorName => "TestEvidenceWorkItems";

        public IReadOnlyCollection<string> ProjectedKinds => [WorkSource.EvidenceRequest];

        public EventStreamPattern SourcePattern(Uuid tenantId) =>
            EventStreamPattern.ForPattern(tenantId.ToString(), "evidence-requests");

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);

        public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
            Uuid programId, CancellationToken ct = default) =>
            ValueTask.FromResult(Result<IReadOnlyList<WorkCandidate>>.Success([]));
    }
}
