using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class FitzControlDecisionWorkItemDirectoryTests
{
    [Fact]
    public async Task ShouldProjectReviewApprovalAndCompletionGivenControlDecisionLifecycle()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        fixture.Permissions.Managers.Add(fixture.ReviewerMemberId);
        fixture.Permissions.Managers.Add(fixture.OwnerMemberId);
        var controlId = Uuid.CreateVersion4();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-3);
        var reviewAssignmentId = Uuid.CreateVersion4();
        var approvalAssignmentId = Uuid.CreateVersion4();
        var scope = new ResponsibilityScope("control", controlId,
            ControlVersionIds.Initial(controlId), 1);
        await ApplyControlAsync(fixture, controlId, control =>
        {
            Assert.True(control.Create(fixture.ProgramId, Uuid.CreateVersion4(), "AC-PROJ",
                Content(), fixture.LeadMemberId, "Lead", createdAt).IsSuccess);
            Assign(control, fixture, scope, reviewAssignmentId, fixture.ReviewerMemberId,
                ResponsibilityType.AssignedReviewer, createdAt);
            Assign(control, fixture, scope, approvalAssignmentId, fixture.ApproverMemberId,
                ResponsibilityType.PolicyApprover, createdAt);
            Assign(control, fixture, scope, Uuid.CreateVersion4(), fixture.OwnerMemberId,
                ResponsibilityType.ControlOwner, createdAt);
            return Result.Success;
        });

        await using var scopeServices = fixture.Provider.CreateAsyncScope();
        var services = scopeServices.ServiceProvider;
        var events = services.GetRequiredService<IDomainEventReader>();
        var kvClient = new InMemoryKvClient();
        var directory = new FitzControlDecisionWorkItemDirectory(kvClient,
            services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<OperatingAuthority>(),
            new ControlActivationReleaseGate(true), new ControlLifecycleReleaseGate(true));
        await ProjectPendingEventsAsync(directory, events, fixture.TenantId);
        var initial = Assert.Single((await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, createdAt, DateOnly.MaxValue, null)).Value);
        var activationDisabled = new FitzControlDecisionWorkItemDirectory(kvClient,
            services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<OperatingAuthority>(),
            new ControlActivationReleaseGate(false), new ControlLifecycleReleaseGate(true));
        Assert.Empty((await activationDisabled.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, createdAt, DateOnly.MaxValue, null)).Value);

        // Act
        var reviewId = Uuid.CreateVersion4();
        var reviewedAt = createdAt.AddMinutes(1);
        await ApplyControlAsync(fixture, controlId, control =>
        {
            Assert.Null(control.Review(fixture.ProgramId, control.Revision, reviewId, "accept",
                "Reviewed against the source procedure.", fixture.ReviewerMemberId, "Reviewer",
                reviewedAt));
            return Result.Success;
        });
        await ProjectPendingEventsAsync(directory, events, fixture.TenantId);
        var approval = Assert.Single((await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, reviewedAt, DateOnly.MaxValue, null)).Value);

        var approvedAt = reviewedAt.AddMinutes(1);
        await ApplyControlAsync(fixture, controlId, control =>
        {
            Assert.Null(control.Approve(fixture.ProgramId, control.Revision,
                Uuid.CreateVersion4(), reviewId, fixture.Today, "Approved.",
                new HashSet<Uuid> { fixture.OwnerMemberId }, fixture.ApproverMemberId,
                "Approver", approvedAt));
            return Result.Success;
        });
        await ProjectPendingEventsAsync(directory, events, fixture.TenantId);
        var completed = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            approvedAt, DateOnly.MaxValue, null);
        Assert.Empty(completed.Value);

        var retirementId = ControlVersionIds.Retirement(controlId,
            ControlVersionIds.Initial(controlId));
        var retirementScope = new ResponsibilityScope("control", controlId, retirementId, 1);
        var retirementProposedAt = approvedAt.AddMinutes(1);
        await ApplyControlAsync(fixture, controlId, control =>
        {
            Assert.Null(control.ProposeRetirement(fixture.ProgramId,
                ControlVersionIds.Initial(controlId), fixture.Today.AddDays(30),
                "The process has been retired.", fixture.LeadMemberId, "Lead",
                retirementProposedAt));
            Assign(control, fixture, retirementScope, Uuid.CreateVersion4(),
                fixture.ReviewerMemberId, ResponsibilityType.AssignedReviewer,
                retirementProposedAt);
            Assign(control, fixture, retirementScope, Uuid.CreateVersion4(),
                fixture.ApproverMemberId, ResponsibilityType.PolicyApprover,
                retirementProposedAt);
            return Result.Success;
        });
        await ProjectPendingEventsAsync(directory, events, fixture.TenantId);
        var retirementReview = Assert.Single((await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, retirementProposedAt, DateOnly.MaxValue, null)).Value);

        var retirementReviewId = Uuid.CreateVersion4();
        var retirementReviewedAt = retirementProposedAt.AddMinutes(1);
        await ApplyControlAsync(fixture, controlId, control =>
        {
            Assert.Null(control.Review(fixture.ProgramId, control.Revision, retirementReviewId,
                "accept", "The impact was reviewed.", fixture.ReviewerMemberId, "Reviewer",
                retirementReviewedAt));
            return Result.Success;
        });
        await ProjectPendingEventsAsync(directory, events, fixture.TenantId);
        var retirementApproval = Assert.Single((await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, retirementReviewedAt, DateOnly.MaxValue, null)).Value);
        var lifecycleDisabled = new FitzControlDecisionWorkItemDirectory(kvClient,
            services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<OperatingAuthority>(),
            new ControlActivationReleaseGate(true), new ControlLifecycleReleaseGate(false));
        Assert.Empty((await lifecycleDisabled.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, retirementReviewedAt, DateOnly.MaxValue, null)).Value);

        var retiredAt = retirementReviewedAt.AddMinutes(1);
        await ApplyControlAsync(fixture, controlId, control =>
        {
            Assert.Null(control.Retire(fixture.ProgramId, control.Revision,
                Uuid.CreateVersion4(), retirementReviewId, "impact-digest", "Retired.",
                fixture.ApproverMemberId, "Approver", retiredAt));
            return Result.Success;
        });
        await ProjectPendingEventsAsync(directory, events, fixture.TenantId);
        var retired = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            retiredAt, DateOnly.MaxValue, null);

        // Assert
        Assert.Equal("control_draft_review", initial.Kind);
        Assert.Equal(fixture.ReviewerMemberId, initial.Responsible.Id);
        Assert.Equal(ControlVersionIds.Initial(controlId), initial.SourceId);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"controls/{controlId}/draft/reviews", initial.ActionPath);
        Assert.Equal("control_draft_approval", approval.Kind);
        Assert.Equal(fixture.ApproverMemberId, approval.Responsible.Id);
        Assert.NotEqual(initial.WorkItemId, approval.WorkItemId);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"controls/{controlId}/draft/approvals", approval.ActionPath);
        Assert.Equal("control_retirement_review", retirementReview.Kind);
        Assert.Equal("review", retirementReview.NextAction);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"controls/{controlId}/draft/reviews", retirementReview.ActionPath);
        Assert.Equal("control_retirement_approval", retirementApproval.Kind);
        Assert.Equal("approve", retirementApproval.NextAction);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"controls/{controlId}/retirements", retirementApproval.ActionPath);
        Assert.Empty(retired.Value);
        Assert.Equal(await CountProjectedEventsAsync(events, fixture.TenantId),
            await directory.LoadRevisionAsync(fixture.TenantId));
        Assert.Equal(await ReadControlCheckpointAsync(events, fixture.TenantId),
            await directory.LoadCheckpointAsync(fixture.TenantId));
        Assert.Empty((await directory.LoadProgramAsync(fixture.TenantId,
            Uuid.CreateVersion4(), approvedAt, DateOnly.MaxValue, null)).Value);
        Assert.Empty((await directory.LoadProgramAsync(Uuid.CreateVersion4(), fixture.ProgramId,
            approvedAt, DateOnly.MaxValue, null)).Value);
    }

    [Fact]
    public async Task ShouldFallBackToProgramReviewersGivenAssignedReviewerLacksManagementRights()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var controlId = Uuid.CreateVersion4();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var responsibilityScope = new ResponsibilityScope("control", controlId,
            ControlVersionIds.Initial(controlId), 1);
        await ApplyControlAsync(fixture, controlId, control =>
        {
            Assert.True(control.Create(fixture.ProgramId, Uuid.CreateVersion4(), "AC-FALLBACK",
                Content(), fixture.LeadMemberId, "Lead", createdAt).IsSuccess);
            Assign(control, fixture, responsibilityScope, Uuid.CreateVersion4(),
                fixture.ReviewerMemberId, ResponsibilityType.AssignedReviewer, createdAt);
            return Result.Success;
        });

        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var events = services.GetRequiredService<IDomainEventReader>();
        var directory = new FitzControlDecisionWorkItemDirectory(new InMemoryKvClient(),
            services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<OperatingAuthority>(),
            new ControlActivationReleaseGate(true), new ControlLifecycleReleaseGate(true));

        // Act
        await ProjectPendingEventsAsync(directory, events, fixture.TenantId);
        var item = Assert.Single((await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, createdAt, DateOnly.MaxValue, null)).Value);

        // Assert
        Assert.Equal(OperatingAuthority.ProgramReviewerHolder, item.Responsible.Kind);
    }

    static async Task ApplyControlAsync(OperationsFixture fixture, Uuid controlId,
        Func<ControlDraft, Result> apply) => await ProgramManagementServices.SeedAsync(
        fixture.Provider, new ControlDraft(fixture.TenantId, controlId), apply);

    static void Assign(ControlDraft control, OperationsFixture fixture,
        ResponsibilityScope scope, Uuid assignmentId, Uuid memberId, ResponsibilityType type,
        DateTimeOffset at)
    {
        Assert.Null(control.AssignResponsibility(scope, assignmentId, memberId, type,
            fixture.LeadMemberId, "Lead", at, at.AddMinutes(-1), null, []));
    }

    static async Task ProjectPendingEventsAsync(FitzControlDecisionWorkItemDirectory directory,
        IDomainEventReader events, Uuid tenantId)
    {
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString(), "controls");
        var identity = new CheckpointIdentity(FitzControlDecisionWorkItemDirectory.ProjectorName,
            pattern);
        var checkpoint = await directory.LoadCheckpointAsync(tenantId);
        await foreach (var record in events.ReadAsync(pattern, checkpoint.Cursor,
                           CancellationToken.None))
        {
            await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                checkpoint));
            await directory.ApplyAsync(record.Event);
            checkpoint = new ProjectionCheckpoint(record.NextCursor);
            await batch.CommitAsync(checkpoint);
        }
    }

    static async Task<ProjectionCheckpoint> ReadControlCheckpointAsync(IDomainEventReader events,
        Uuid tenantId)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(
                           tenantId.ToString(), "controls"), cursor, CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    static async Task<long> CountProjectedEventsAsync(IDomainEventReader events, Uuid tenantId)
    {
        var count = 0L;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(
                           tenantId.ToString(), "controls"), EventCursor.Start,
                           CancellationToken.None))
        {
            if (record.Event is ControlDraftCreated or ControlDraftRevised or
                ControlDraftDiscarded or ControlSuccessorProposed or ControlProposalWithdrawn or
                ControlReviewed or ControlApproved or ControlRetirementProposed or ControlRetired ||
                record.Event is ResponsibilityAssigned assigned &&
                assigned.Scope.RecordType == "control" ||
                record.Event is ResponsibilityRevoked revoked &&
                revoked.Scope.RecordType == "control")
                count++;
        }
        return count;
    }

    static ControlDraftContent Content() => new("Access review", "Review access",
        "Management reviews user access.", "Review access monthly.", ["Signed review record"]);
}
