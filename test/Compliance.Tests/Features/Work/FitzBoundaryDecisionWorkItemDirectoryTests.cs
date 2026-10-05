using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class FitzBoundaryDecisionWorkItemDirectoryTests
{
    [Fact]
    public async Task ShouldReturnOneReviewGivenDuplicateReviewerAssignments()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var boundaryId = Uuid.CreateVersion4();
        var draftVersionId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var changedAt = now.AddMinutes(-5);
        var content = Content();
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.True(boundary.Create(fixture.ProgramId, draftVersionId, content,
                    fixture.LeadMemberId, "Lead", changedAt).IsSuccess);
                for (var assignment = 0; assignment < 2; assignment++)
                    Assert.Null(boundary.AssignResponsibility(new ResponsibilityScope("boundary",
                            boundaryId, draftVersionId, 1), Uuid.CreateVersion4(),
                        fixture.ReviewerMemberId, ResponsibilityType.AssignedReviewer,
                        fixture.LeadMemberId, "Lead", changedAt, changedAt, null, []));
                return Result.Success;
            });
        fixture.Boundaries.Add(new BoundaryView(fixture.TenantId, boundaryId, fixture.ProgramId,
            new BoundaryVersionView(fixture.TenantId, boundaryId, fixture.ProgramId,
                draftVersionId, 1, content, "draft", null, fixture.LeadMemberId, "Lead",
                changedAt), null, null, 1));
        await fixture.CatchUpBoundaryDirectoryAsync();

        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var sourceReader = sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var directory = new FitzBoundaryDecisionWorkItemDirectory(new InMemoryKvClient(),
            sourceReader);

        // Act
        var live = await BoundaryDecisionWork.LoadAsync(sourceReader, fixture.Boundaries,
            fixture.TenantId, fixture.ProgramId, now, null, CancellationToken.None);
        await ProjectAsync(directory, events, fixture.TenantId,
            new BoundaryDraftCreated(fixture.TenantId, boundaryId, fixture.ProgramId,
                draftVersionId, content, fixture.LeadMemberId, "Lead", changedAt));
        var projected = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            now, DateOnly.MaxValue, null);

        // Assert
        var liveItem = Assert.Single(live.Value);
        var projectedItem = Assert.Single(projected.Value);
        Assert.Equal(liveItem.WorkItemId, projectedItem.WorkItemId);
        Assert.Equal(fixture.ReviewerMemberId, projectedItem.Responsible.Id);
    }

    [Fact]
    public async Task ShouldProjectBoundaryDecisionLifecycleGivenAssignedDraftAndAcceptedReview()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var boundaryId = Uuid.CreateVersion4();
        var draftVersionId = Uuid.CreateVersion4();
        var reviewerAssignmentId = Uuid.CreateVersion4();
        var approverAssignmentId = Uuid.CreateVersion4();
        var reviewDecisionId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow.AddHours(-2);
        var content = Content();
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.True(boundary.Create(fixture.ProgramId, draftVersionId, content,
                    fixture.LeadMemberId, "Lead", now).IsSuccess);
                Assert.Null(boundary.AssignResponsibility(new ResponsibilityScope("boundary",
                        boundaryId, draftVersionId, 1), reviewerAssignmentId,
                    fixture.ReviewerMemberId, ResponsibilityType.AssignedReviewer,
                    fixture.LeadMemberId, "Lead", now, now.AddHours(1), now.AddHours(5), []));
                Assert.Null(boundary.AssignResponsibility(new ResponsibilityScope("boundary",
                        boundaryId, draftVersionId, 1), approverAssignmentId,
                    fixture.ApproverMemberId, ResponsibilityType.PolicyApprover,
                    fixture.LeadMemberId, "Lead", now, now, null, []));
                return Result.Success;
            });
        fixture.Boundaries.Add(new BoundaryView(fixture.TenantId, boundaryId, fixture.ProgramId,
            new BoundaryVersionView(fixture.TenantId, boundaryId, fixture.ProgramId,
                draftVersionId, 1, content, "draft", null, fixture.LeadMemberId, "Lead", now),
            null, null, 1));
        await fixture.CatchUpBoundaryDirectoryAsync();

        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzBoundaryDecisionWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>());
        var consistency = new WorkQueueReadConsistency(fixture.Boundaries, events, [directory]);
        var behind = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);

        // Act
        await ProjectAsync(directory, events, fixture.TenantId,
            new BoundaryDraftCreated(fixture.TenantId, boundaryId, fixture.ProgramId,
                draftVersionId, content, fixture.LeadMemberId, "Lead", now));
        var caughtUp = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);
        var beforeReviewer = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            now.AddHours(1).AddTicks(-1), DateOnly.MaxValue, null);
        var review = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            now.AddHours(1), DateOnly.MaxValue, null);
        var foreignProgram = await directory.LoadProgramAsync(fixture.TenantId,
            Uuid.CreateVersion4(), now.AddHours(1), DateOnly.MaxValue, null);
        var foreignTenant = await directory.LoadProgramAsync(Uuid.CreateVersion4(),
            fixture.ProgramId, now.AddHours(1), DateOnly.MaxValue, null);
        await using var queueScope = fixture.Provider.CreateAsyncScope();
        var queueReader = new WorkQueueReader(
            queueScope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            queueScope.ServiceProvider.GetRequiredService<OperatingAuthority>(), TimeProvider.System,
            consistency, boundaries: fixture.Boundaries, accountableWorkItems: [directory]);
        var queueResult = await queueReader.ReadAsync(fixture.TenantId, fixture.ProgramId,
            new OperationsActor(fixture.ReviewerUserId, fixture.ReviewerMemberId, "Reviewer"), 30,
            CancellationToken.None);

        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.Null(boundary.Review(draftVersionId, 1, reviewDecisionId, "accept",
                    "Reviewed independently.", fixture.ReviewerMemberId, "Reviewer",
                    now.AddHours(2)));
                return Result.Success;
            });
        await ProjectAsync(directory, events, fixture.TenantId,
            new BoundaryReviewed(fixture.TenantId, boundaryId, draftVersionId, 1,
                reviewDecisionId, "accept", fixture.ReviewerMemberId, "Reviewer",
                "Reviewed independently.", now.AddHours(2)));
        var approval = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            now.AddHours(2), DateOnly.MaxValue, null);

        var approvalDecisionId = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.Null(boundary.Approve(draftVersionId, 1, approvalDecisionId,
                    reviewDecisionId, fixture.Today.AddDays(1), "Approved.", "impact-digest",
                    fixture.ApproverMemberId, "Approver", now.AddHours(3)));
                return Result.Success;
            });
        await ProjectAsync(directory, events, fixture.TenantId,
            new BoundaryApproved(fixture.TenantId, boundaryId, draftVersionId, 1,
                approvalDecisionId, reviewDecisionId, fixture.ApproverMemberId, "Approver",
                "Approved.", fixture.Today.AddDays(1), now.AddHours(3), "impact-digest"));
        var completed = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            now.AddHours(3), DateOnly.MaxValue, null);

        var successorDraftVersionId = Uuid.CreateVersion4();
        var successorAt = now.AddHours(4);
        var successorContent = Content("Successor SOC 2 system boundary");
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.Null(boundary.ProposeSuccessor(draftVersionId, successorDraftVersionId,
                    successorContent, fixture.LeadMemberId, "Lead", successorAt));
                Assert.Null(boundary.AssignResponsibility(new ResponsibilityScope("boundary",
                        boundaryId, successorDraftVersionId, 1), Uuid.CreateVersion4(),
                    fixture.ReviewerMemberId, ResponsibilityType.AssignedReviewer,
                    fixture.LeadMemberId, "Lead", successorAt, successorAt, null, []));
                return Result.Success;
            });
        await ProjectAsync(directory, events, fixture.TenantId,
            new BoundarySuccessorProposed(fixture.TenantId, boundaryId,
                successorDraftVersionId, draftVersionId, successorContent,
                fixture.LeadMemberId, "Lead", successorAt));
        var successor = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            successorAt, DateOnly.MaxValue, null);

        // Assert
        Assert.False(behind.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, behind.Error.Kind);
        Assert.True(behind.Error.IsTransient);
        Assert.True(caughtUp.IsSuccess);
        Assert.Empty(beforeReviewer.Value);
        Assert.Empty(foreignProgram.Value);
        Assert.Empty(foreignTenant.Value);
        var reviewItem = Assert.Single(review.Value);
        Assert.Equal("boundary_review", reviewItem.Kind);
        Assert.Equal(fixture.ReviewerMemberId, reviewItem.Responsible.Id);
        Assert.Equal(draftVersionId, reviewItem.SourceId);
        Assert.Equal(now, reviewItem.CreatedAt);
        Assert.True(queueResult.IsSuccess);
        Assert.Single(queueResult.Value.Entries,
            entry => entry.Item.Kind == "boundary_review");
        Assert.Equal("boundary_approval", Assert.Single(approval.Value).Kind);
        Assert.Equal(fixture.ApproverMemberId, Assert.Single(approval.Value).Responsible.Id);
        Assert.Empty(completed.Value);
        var successorItem = Assert.Single(successor.Value);
        Assert.Equal("boundary_review", successorItem.Kind);
        Assert.Equal(successorDraftVersionId, successorItem.SourceId);
        Assert.Equal(successorAt, successorItem.CreatedAt);
        Assert.Equal(4, await directory.LoadRevisionAsync(fixture.TenantId));
    }

    [Fact]
    public async Task ShouldReopenReviewForRevisedDraftGivenChangesRequestedDecision()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var boundaryId = Uuid.CreateVersion4();
        var draftVersionId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow.AddMinutes(-5);
        var content = Content();
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.True(boundary.Create(fixture.ProgramId, draftVersionId, content,
                    fixture.LeadMemberId, "Lead", now).IsSuccess);
                Assert.Null(boundary.AssignResponsibility(new ResponsibilityScope("boundary",
                        boundaryId, draftVersionId, 1), Uuid.CreateVersion4(),
                    fixture.ReviewerMemberId, ResponsibilityType.AssignedReviewer,
                    fixture.LeadMemberId, "Lead", now, now, null, []));
                return Result.Success;
            });

        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzBoundaryDecisionWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>());
        await ProjectAsync(directory, events, fixture.TenantId,
            new BoundaryDraftCreated(fixture.TenantId, boundaryId, fixture.ProgramId,
                draftVersionId, content, fixture.LeadMemberId, "Lead", now));
        var original = Assert.Single((await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, now, DateOnly.MaxValue, null)).Value);

        // Act
        var reviewDecisionId = Uuid.CreateVersion4();
        var requestedAt = now.AddMinutes(1);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.Null(boundary.Review(draftVersionId, 1, reviewDecisionId,
                    "request_changes", "Clarify the service boundary.", fixture.ReviewerMemberId,
                    "Reviewer", requestedAt));
                return Result.Success;
            });
        await ProjectAsync(directory, events, fixture.TenantId,
            new BoundaryReviewed(fixture.TenantId, boundaryId, draftVersionId, 1,
                reviewDecisionId, "request_changes", fixture.ReviewerMemberId, "Reviewer",
                "Clarify the service boundary.", requestedAt));
        var afterChangesRequested = await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, requestedAt, DateOnly.MaxValue, null);

        var revisedContent = Content("Updated SOC 2 system boundary");
        var revisedAt = requestedAt.AddMinutes(1);
        var revisedVersionRevision = 2L;
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.Null(boundary.Revise(draftVersionId, 1, revisedContent,
                    fixture.LeadMemberId, "Lead", revisedAt));
                Assert.Null(boundary.AssignResponsibility(new ResponsibilityScope("boundary",
                        boundaryId, draftVersionId, revisedVersionRevision), Uuid.CreateVersion4(),
                    fixture.ReviewerMemberId, ResponsibilityType.AssignedReviewer,
                    fixture.LeadMemberId, "Lead", revisedAt, revisedAt, null, []));
                return Result.Success;
            });
        await ProjectAsync(directory, events, fixture.TenantId,
            new BoundaryDraftRevised(fixture.TenantId, boundaryId, draftVersionId,
                revisedVersionRevision, revisedContent, fixture.LeadMemberId, "Lead", revisedAt));
        var revised = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            revisedAt, DateOnly.MaxValue, null);

        // Assert
        Assert.Empty(afterChangesRequested.Value);
        var reopened = Assert.Single(revised.Value);
        Assert.Equal("boundary_review", reopened.Kind);
        Assert.NotEqual(original.WorkItemId, reopened.WorkItemId);
        Assert.Contains("revision 2", reopened.Reason, StringComparison.Ordinal);
        Assert.Equal(revisedAt, reopened.CreatedAt);
    }

    [Fact]
    public async Task ShouldApplyEffectiveAndRevokedTimesAtReadGivenBoundaryResponsibilityChanges()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var boundaryId = Uuid.CreateVersion4();
        var draftVersionId = Uuid.CreateVersion4();
        var assignmentId = Uuid.CreateVersion4();
        var now = At(fixture.Today).AddHours(8);
        var effectiveFrom = now.AddHours(1);
        var revokedAt = now.AddHours(3);
        var content = Content();
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.True(boundary.Create(fixture.ProgramId, draftVersionId, content,
                    fixture.LeadMemberId, "Lead", now).IsSuccess);
                Assert.Null(boundary.AssignResponsibility(new ResponsibilityScope("boundary",
                        boundaryId, draftVersionId, 1), assignmentId,
                    fixture.ReviewerMemberId, ResponsibilityType.AssignedReviewer,
                    fixture.LeadMemberId, "Lead", now, effectiveFrom, null, []));
                return Result.Success;
            });

        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzBoundaryDecisionWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>());

        // Act
        await ProjectAsync(directory, events, fixture.TenantId,
            new BoundaryDraftCreated(fixture.TenantId, boundaryId, fixture.ProgramId,
                draftVersionId, content, fixture.LeadMemberId, "Lead", now));
        var beforeStart = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            effectiveFrom.AddTicks(-1), DateOnly.MaxValue, null);
        var atStart = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            effectiveFrom, DateOnly.MaxValue, null);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.Null(boundary.RevokeResponsibility(new ResponsibilityScope("boundary",
                        boundaryId, draftVersionId, 1), assignmentId, fixture.LeadMemberId,
                    "Lead", revokedAt, "Responsibility ended."));
                return Result.Success;
            });
        await ProjectAsync(directory, events, fixture.TenantId,
            new ResponsibilityRevoked(fixture.TenantId, assignmentId,
                new ResponsibilityScope("boundary", boundaryId, draftVersionId, 1),
                fixture.LeadMemberId, revokedAt, "Responsibility ended.", "Lead"));
        var beforeRevocation = await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, revokedAt.AddTicks(-1), DateOnly.MaxValue, null);
        var atRevocation = await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, revokedAt, DateOnly.MaxValue, null);

        // Assert
        Assert.Empty(beforeStart.Value);
        Assert.Equal(fixture.ReviewerMemberId, Assert.Single(atStart.Value).Responsible.Id);
        Assert.Equal(fixture.ReviewerMemberId,
            Assert.Single(beforeRevocation.Value).Responsible.Id);
        Assert.Empty(atRevocation.Value);
    }

    static async Task ProjectAsync(FitzBoundaryDecisionWorkItemDirectory directory,
        IDomainEventReader events, Uuid tenantId, DomainEvent domainEvent)
    {
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString(), "boundaries");
        var identity = new CheckpointIdentity(FitzBoundaryDecisionWorkItemDirectory.ProjectorName,
            pattern);
        var current = await directory.LoadCheckpointAsync(tenantId);
        var source = await ReadBoundaryCheckpointAsync(events, tenantId);
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            current));
        await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(source);
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

    static BoundaryContent Content(string statement = "SOC 2 system boundary") =>
        new(statement, "readiness", ["security"], []);

    static DateTimeOffset At(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
