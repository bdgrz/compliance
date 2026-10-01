using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WorkAssignmentTests
{
    [Fact]
    public async Task ShouldClaimTeamWorkGivenEligibleTeamMember()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.AddToTeamAsync(fixture.BackupUserId);
        await fixture.AddToTeamAsync(fixture.OwnerUserId);
        await fixture.PlanAsync(owner: new OperatingHolder("team", fixture.TeamId));
        var unassigned = await fixture.AsAsync(fixture.BackupUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var item = unassigned.Items[0];
        Assert.Null(item.AssigneeMemberId);
        await fixture.Scenario(fixture.OutsiderUserId)
            .When(new ClaimWorkItem(fixture.TenantId, fixture.ProgramId, item.WorkItemId, 0))
            .ExpectFailure(RequestErrorKind.NotFound);

        // Act
        var claimed = await fixture.AsAsync(fixture.BackupUserId,
            new ClaimWorkItem(fixture.TenantId, fixture.ProgramId, item.WorkItemId, 0));

        // Assert
        Assert.Equal(fixture.BackupMemberId, claimed.Item.AssigneeMemberId);
        var entry = Assert.Single(claimed.History);
        Assert.Equal("claim", entry.Action);
        Assert.Equal(fixture.BackupMemberId.ToString(), entry.By.Id);
        await fixture.Scenario(fixture.OwnerUserId)
            .When(new ClaimWorkItem(fixture.TenantId, fixture.ProgramId, item.WorkItemId, 1))
            .ExpectFailure(RequestErrorKind.Conflict);
        var mine = await fixture.AsAsync(fixture.BackupUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));
        Assert.Contains(mine.Items, work => work.WorkItemId == item.WorkItemId);
        var team = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "team"));
        Assert.Contains(team.Items, work => work.WorkItemId == item.WorkItemId);
    }

    [Fact]
    public async Task ShouldRejectReviewAssignmentGivenPerformerSeparationOfDuties()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var missed = (await fixture.OccurrencesAsync("missed"))[0];
        await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(missed));
        var review = Assert.Single((await fixture.AsAsync(fixture.ReviewerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId))).Items);

        // Act
        var performer = fixture.Scenario(fixture.LeadUserId).When(new AssignWorkItem(
            fixture.TenantId, fixture.ProgramId, review.WorkItemId, 0, fixture.OwnerMemberId));

        // Assert
        await performer.ExpectFailure(RequestErrorKind.Validation);
        await fixture.Scenario(fixture.ReviewerUserId).When(new AssignWorkItem(fixture.TenantId,
                fixture.ProgramId, review.WorkItemId, 0, fixture.ReviewerMemberId))
            .ExpectFailure(RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldReassignWithHistoryGivenProgramManager()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync(backup: new OperatingHolder("member", fixture.BackupMemberId));
        var item = (await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId))).Items[0];

        // Act
        var reassigned = await fixture.AsAsync(fixture.LeadUserId, new AssignWorkItem(
            fixture.TenantId, fixture.ProgramId, item.WorkItemId, 0, fixture.BackupMemberId,
            "Owner is on leave."));

        // Assert
        Assert.Equal(fixture.BackupMemberId, reassigned.Item.AssigneeMemberId);
        var entry = Assert.Single(reassigned.History);
        Assert.Equal("reassign", entry.Action);
        Assert.Equal(fixture.OwnerMemberId, entry.PreviousAssigneeMemberId);
        Assert.Equal(fixture.LeadMemberId.ToString(), entry.By.Id);
        await fixture.Scenario(fixture.LeadUserId).When(new AssignWorkItem(fixture.TenantId,
                fixture.ProgramId, item.WorkItemId, 1, fixture.Member(fixture.OutsiderUserId)))
            .ExpectFailure(RequestErrorKind.Validation);
        await fixture.Scenario(fixture.LeadUserId).When(new AssignWorkItem(fixture.TenantId,
                fixture.ProgramId, item.WorkItemId, 0, fixture.OwnerMemberId))
            .ExpectFailure(RequestErrorKind.Conflict);
    }

    [Fact]
    public async Task ShouldDelegateGivenCurrentAssigneeAndEligibleTarget()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync(backup: new OperatingHolder("member", fixture.BackupMemberId));
        var item = (await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId))).Items[0];
        await fixture.Scenario(fixture.OwnerUserId).When(new DelegateWorkItem(fixture.TenantId,
                fixture.ProgramId, item.WorkItemId, 0, fixture.Member(fixture.OutsiderUserId),
                "Busy."))
            .ExpectFailure(RequestErrorKind.Validation);
        await fixture.Scenario(fixture.BackupUserId).When(new DelegateWorkItem(fixture.TenantId,
                fixture.ProgramId, item.WorkItemId, 0, fixture.BackupMemberId, "Mine now."))
            .ExpectFailure(RequestErrorKind.Forbidden);

        // Act
        var delegated = await fixture.AsAsync(fixture.OwnerUserId, new DelegateWorkItem(
            fixture.TenantId, fixture.ProgramId, item.WorkItemId, 0, fixture.BackupMemberId,
            "Travelling this week."));

        // Assert
        Assert.Equal(fixture.BackupMemberId, delegated.Item.AssigneeMemberId);
        Assert.Equal("delegate", Assert.Single(delegated.History).Action);
        var backup = await fixture.AsAsync(fixture.BackupUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));
        Assert.Contains(backup.Items, work => work.WorkItemId == item.WorkItemId);
    }

    [Fact]
    public async Task ShouldEscalateOnceWithoutReassignmentGivenAssignee()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId, fixture.Today.AddDays(3));
        var item = Assert.Single((await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId))).Items);

        // Act
        var escalated = await fixture.AsAsync(fixture.OwnerUserId, new EscalateWorkItem(
            fixture.TenantId, fixture.ProgramId, item.WorkItemId, 0, "Blocked by vendor."));

        // Assert
        Assert.True(escalated.Item.Escalated);
        Assert.Equal("member", escalated.Item.EscalatedBy);
        Assert.Equal(fixture.OwnerMemberId, escalated.Item.AssigneeMemberId);
        await fixture.Scenario(fixture.OwnerUserId).When(new EscalateWorkItem(fixture.TenantId,
                fixture.ProgramId, item.WorkItemId, 1, "Again."))
            .ExpectFailure(RequestErrorKind.Conflict);
        var lead = await fixture.AsAsync(fixture.LeadUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "escalated"));
        Assert.Equal(item.WorkItemId, Assert.Single(lead.Items).WorkItemId);
        Assert.Equal(1, lead.Counts.Escalated);
    }

    [Fact]
    public async Task ShouldEscalateBySystemGivenSevenDaysOverdue()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId,
            fixture.Today.AddDays(-7), fixture.Today.AddDays(-6));

        // Act
        var lead = await fixture.AsAsync(fixture.LeadUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "escalated"));

        // Assert
        var item = Assert.Single(lead.Items);
        Assert.Equal("system", item.EscalatedBy);
        Assert.Equal(fixture.OwnerMemberId, item.AssigneeMemberId);
        await fixture.Scenario(fixture.OwnerUserId).When(new EscalateWorkItem(fixture.TenantId,
                fixture.ProgramId, item.WorkItemId, 0, "Duplicate."))
            .ExpectFailure(RequestErrorKind.Conflict);
    }
}
