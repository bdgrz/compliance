using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class FitzControlOperatingPlanWorkItemDirectoryTests
{
    [Fact]
    public async Task ShouldProjectPendingApprovalAndRemoveAfterApprovalGivenProposedPlan()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.ProposeAsync(0);
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzControlOperatingPlanWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>());
        var identity = new CheckpointIdentity(FitzControlOperatingPlanWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(fixture.TenantId.ToString(), "control-operations"));
        var proposedCheckpoint = new ProjectionCheckpoint(new EventCursor("plan-proposed"));
        var approvedCheckpoint = new ProjectionCheckpoint(new EventCursor("plan-approved"));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ControlOperatingPlanProposed(fixture.TenantId,
                fixture.ProgramId, fixture.ControlId, plan.Revision, plan));
            await batch.CommitAsync(proposedCheckpoint);
        }
        var pending = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            CancellationToken.None);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         proposedCheckpoint)))
        {
            await directory.ApplyAsync(new ControlOperatingPlanApproved(fixture.TenantId,
                fixture.ProgramId, fixture.ControlId, plan.Revision + 1, plan.PlanVersionId,
                fixture.ApproverMemberId,
                Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(
                    fixture.ApproverMemberId, "Approver"), plan.ProposedAt.AddMinutes(1),
                "Independently approved.", null, []));
            await batch.CommitAsync(approvedCheckpoint);
        }
        var approved = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            CancellationToken.None);

        // Assert
        Assert.True(pending.IsSuccess);
        var item = Assert.Single(pending.Value);
        Assert.Equal(WorkSource.ControlOperatingPlanApproval, item.Kind);
        Assert.Equal(plan.PlanVersionId, item.SourceId);
        Assert.Equal(fixture.ControlId, item.ControlId);
        Assert.Equal("Approve operating plan for AC-OPS", item.Summary);
        Assert.Equal("A control operating plan is awaiting independent approval.", item.Reason);
        Assert.Equal("approve", item.NextAction);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
            $"controls/{fixture.ControlId}/operating-plan/proposals/{plan.PlanVersionId}/approvals",
            item.ActionPath);
        Assert.Equal(new OperatingHolder(OperatingAuthority.ProgramManagerHolder,
            fixture.ProgramId), item.Responsible);
        Assert.Contains(fixture.LeadMemberId, item.Excluded);
        Assert.Equal(plan.ProposedAt, item.CreatedAt);
        Assert.Equal(WorkCandidate.IdFor(plan.PlanVersionId,
            WorkSource.ControlOperatingPlanApproval), item.WorkItemId);
        Assert.True(approved.IsSuccess);
        Assert.Empty(approved.Value);
        Assert.Equal(2, await directory.LoadRevisionAsync(fixture.TenantId));
        Assert.Equal(approvedCheckpoint,
            await directory.LoadCheckpointAsync(fixture.TenantId));
    }

    [Fact]
    public async Task ShouldHideStaleApprovalGivenControlRetired()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.ProposeAsync(0);
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzControlOperatingPlanWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>());
        var identity = new CheckpointIdentity(FitzControlOperatingPlanWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(fixture.TenantId.ToString(), "control-operations"));
        var checkpoint = new ProjectionCheckpoint(new EventCursor("plan-proposed"));
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ControlOperatingPlanProposed(fixture.TenantId,
                fixture.ProgramId, fixture.ControlId, plan.Revision, plan));
            await batch.CommitAsync(checkpoint);
        }
        await fixture.RetireControlAsync(fixture.Today);

        // Act
        var work = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            CancellationToken.None);

        // Assert
        Assert.True(work.IsSuccess);
        Assert.Empty(work.Value);
    }

    [Fact]
    public async Task ShouldHideStaleApprovalGivenControlVersionChanged()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.ProposeAsync(0);
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzControlOperatingPlanWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>());
        var identity = new CheckpointIdentity(FitzControlOperatingPlanWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(fixture.TenantId.ToString(), "control-operations"));
        var stalePlan = plan with { ControlVersionId = Uuid.CreateVersion4() };

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ControlOperatingPlanProposed(fixture.TenantId,
                fixture.ProgramId, fixture.ControlId, plan.Revision, stalePlan));
            await batch.CommitAsync(new ProjectionCheckpoint(new EventCursor("stale-plan")));
        }
        var work = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            CancellationToken.None);

        // Assert
        Assert.True(work.IsSuccess);
        Assert.Empty(work.Value);
    }

    [Fact]
    public async Task ShouldIsolatePendingApprovalToItsTenantAndProgramGivenProposal()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.ProposeAsync(0);
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzControlOperatingPlanWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>());
        var identity = new CheckpointIdentity(FitzControlOperatingPlanWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(fixture.TenantId.ToString(), "control-operations"));
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ControlOperatingPlanProposed(fixture.TenantId,
                fixture.ProgramId, fixture.ControlId, plan.Revision, plan));
            await batch.CommitAsync(new ProjectionCheckpoint(new EventCursor("plan-proposed")));
        }

        // Act
        var otherProgram = await directory.LoadProgramAsync(fixture.TenantId,
            Uuid.CreateVersion4(), CancellationToken.None);
        var otherTenant = await directory.LoadProgramAsync(Uuid.CreateVersion4(), fixture.ProgramId,
            CancellationToken.None);

        // Assert
        Assert.True(otherProgram.IsSuccess);
        Assert.Empty(otherProgram.Value);
        Assert.True(otherTenant.IsSuccess);
        Assert.Empty(otherTenant.Value);
    }

    [Fact]
    public async Task ShouldRejectNoninitialPlanRevisionGivenFirstProposal()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.ProposeAsync(0);
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzControlOperatingPlanWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>());
        var identity = new CheckpointIdentity(FitzControlOperatingPlanWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(fixture.TenantId.ToString(), "control-operations"));

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));

        // Assert
        var skipped = plan with { Revision = 2 };
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await directory.ApplyAsync(new ControlOperatingPlanProposed(fixture.TenantId,
                fixture.ProgramId, fixture.ControlId, 2, skipped)));
    }

    [Fact]
    public async Task ShouldAdvanceProjectionRevisionGivenOccurrenceSourceEvent()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzControlOperatingPlanWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>());
        var identity = new CheckpointIdentity(FitzControlOperatingPlanWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(fixture.TenantId.ToString(), "control-operations"));
        var opened = new ControlOccurrenceOpened(fixture.TenantId, fixture.ProgramId,
            fixture.ControlId, Uuid.CreateVersion4(), 1, ControlOperationsLedger.Expected,
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), fixture.Today, fixture.Today,
            fixture.Today.AddDays(1), null,
            new OperatingHolder(OperatingAuthority.MemberHolder, fixture.OwnerMemberId),
            Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(
                fixture.OwnerMemberId, "Owner"), DateTimeOffset.UtcNow);

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(opened);
            await batch.CommitAsync(new ProjectionCheckpoint(new EventCursor("occurrence-opened")));
        }

        // Assert
        Assert.Equal(1, await directory.LoadRevisionAsync(fixture.TenantId));
    }
}
