using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class ControlOperatingPlanWorkTests
{
    [Fact]
    public async Task ShouldShowPendingOperatingPlanApprovalGivenIndependentProgramManager()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.ProposeAsync(0);

        // Act
        var managerQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var sameManagerQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var proposerQueue = await fixture.AsAsync(fixture.LeadUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var otherProgramQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, Uuid.CreateVersion4(), "unassigned"));
        var otherTenantQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(Uuid.CreateVersion4(), fixture.ProgramId, "unassigned"));

        // Assert
        var item = Assert.Single(managerQueue.Items);
        Assert.Equal("control_operating_plan_approval", item.Kind);
        Assert.Equal(plan.PlanVersionId, item.SourceId);
        Assert.Equal(fixture.ControlId, item.ControlId);
        Assert.Equal("approve", item.NextAction);
        Assert.Null(item.AssigneeMemberId);
        Assert.Equal(new OperatingHolder(OperatingAuthority.ProgramReviewerHolder,
            fixture.ProgramId), item.Responsible);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/controls/" +
                     $"{fixture.ControlId}/operating-plan/proposals/{plan.PlanVersionId}/approvals",
            item.ActionPath);
        Assert.Equal(item.WorkItemId, Assert.Single(sameManagerQueue.Items).WorkItemId);
        Assert.Empty(proposerQueue.Items);
        Assert.Empty(otherProgramQueue.Items);
        Assert.Empty(otherTenantQueue.Items);
    }

    [Fact]
    public async Task ShouldRemovePendingOperatingPlanApprovalGivenPlanApproved()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.ProposeAsync(0);
        var before = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));

        // Act
        await fixture.AsAsync(fixture.ApproverUserId, new ApproveControlOperatingPlan(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, plan.Revision,
            plan.PlanVersionId, "Independent approval."));
        var after = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));

        // Assert
        Assert.Contains(before.Items, item => item.SourceId == plan.PlanVersionId &&
                                              item.Kind == "control_operating_plan_approval");
        Assert.DoesNotContain(after.Items,
            item => item.Kind == "control_operating_plan_approval");
    }

    [Fact]
    public async Task ShouldOmitPendingPlanApprovalGivenRetiredControl()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.ProposeAsync(0);
        await fixture.RetireControlAsync(fixture.Today);

        // Act
        var queue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));

        // Assert
        Assert.DoesNotContain(queue.Items,
            item => item.Kind == "control_operating_plan_approval");
    }
}
