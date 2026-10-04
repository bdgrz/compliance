using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
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
    public async Task ShouldAssignRiskCompletionReviewToEligibleReviewerGivenSubmittedCompletion()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var riskId = Uuid.CreateVersion4();
        var actionId = Uuid.CreateVersion4();
        var evidenceId = Uuid.CreateVersion4();
        var submissionId = Uuid.CreateVersion4();
        var lead = ActorReference.ForMember(fixture.LeadMemberId, "Lead");
        var now = DateTimeOffset.UtcNow;
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new EvidenceRequestLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.OpenRequest(evidenceId, "MFA export", "Upload the policy.",
                    fixture.OwnerMemberId, fixture.Today.AddDays(5), null, lead, now));
                return Result.Success;
            });
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new EvidenceRequestLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.Fulfil(evidenceId, 1, Uuid.CreateVersion4(), lead, now));
                return Result.Success;
            });
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new RiskGovernanceLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.AddTreatmentAction(riskId, 0, actionId, "mitigate",
                    "Enforce MFA", "MFA is required for every administrator.",
                    "Identity provider policy export.", fixture.Today.AddDays(9),
                    fixture.OwnerMemberId, [evidenceId], lead, now));
                Assert.Null(ledger.SubmitActionCompletion(riskId, actionId, 1, submissionId,
                    "MFA enforced on all administrators.", [evidenceId], new HashSet<Uuid>
                    {
                        evidenceId,
                    }, fixture.LeadMemberId, lead, now));
                return Result.Success;
            });
        var managerQueue = await fixture.AsAsync(fixture.LeadUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "all"));
        var review = Assert.Single(managerQueue.Items,
            item => item.Kind == "risk_treatment_action_review");

        // Act
        await fixture.Scenario(fixture.LeadUserId).When(new AssignWorkItem(fixture.TenantId,
                fixture.ProgramId, review.WorkItemId, 0, fixture.OwnerMemberId,
                "Action owner cannot review own work."))
            .ExpectFailure(RequestErrorKind.Validation);
        await fixture.Scenario(fixture.LeadUserId).When(new AssignWorkItem(fixture.TenantId,
                fixture.ProgramId, review.WorkItemId, 0, fixture.ReviewerMemberId,
                "Reviewer has no scoped program-management grant."))
            .ExpectFailure(RequestErrorKind.Validation);
        var assigned = await fixture.AsAsync(fixture.LeadUserId, new AssignWorkItem(
            fixture.TenantId, fixture.ProgramId, review.WorkItemId, 0,
            fixture.ApproverMemberId, "Independent completion review."));
        var reviewerQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new RiskGovernanceLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.ReviewActionCompletion(riskId, actionId, 2,
                    Uuid.CreateVersion4(), "accept", "Evidence proves the target state.",
                    fixture.ApproverMemberId,
                    ActorReference.ForMember(fixture.ApproverMemberId, "Approver"),
                    DateTimeOffset.UtcNow, new HashSet<Uuid> { evidenceId }));
                return Result.Success;
            });
        var afterReview = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));

        // Assert
        Assert.Equal(submissionId, review.SourceId);
        Assert.Equal("review", review.NextAction);
        Assert.EndsWith($"/risks/{riskId}/treatment-actions/{actionId}/completion-reviews",
            review.ActionPath, StringComparison.Ordinal);
        Assert.Null(review.AssigneeMemberId);
        Assert.Equal(fixture.ApproverMemberId, assigned.Item.AssigneeMemberId);
        Assert.Contains(reviewerQueue.Items,
            item => item.WorkItemId == review.WorkItemId &&
                    item.AssigneeMemberId == fixture.ApproverMemberId);
        Assert.DoesNotContain(afterReview.Items, item => item.WorkItemId == review.WorkItemId);
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

    [Fact]
    public async Task ShouldNotDefaultReviewToRecorderGivenReviewerRecordedAttestation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var proposed = await fixture.ProposeAsync(0,
            owner: new OperatingHolder("person", fixture.PersonId),
            reviewer: fixture.ApproverMemberId);
        await fixture.AsAsync(fixture.ApproverUserId, new ApproveControlOperatingPlan(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, proposed.Revision,
            proposed.PlanVersionId, "Independent approval."));
        var missed = (await fixture.OccurrencesAsync("missed"))[0];
        await fixture.AsAsync(fixture.ApproverUserId,
            fixture.Attest(missed, personId: fixture.PersonId));

        // Act
        var all = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "all"));

        // Assert
        var review = Assert.Single(all.Items, item => item.Kind == "occurrence_review");
        Assert.Null(review.AssigneeMemberId);
        Assert.DoesNotContain((await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId))).Items,
            item => item.WorkItemId == review.WorkItemId);
        Assert.DoesNotContain(await fixture.AsAsync(fixture.ApproverUserId,
            new ListWorkReminders(fixture.TenantId, fixture.ProgramId)),
            reminder => reminder.WorkItemId == review.WorkItemId && reminder.Kind != "escalated");
        var digest = await fixture.AsAsync(fixture.ApproverUserId,
            new GetWorkDigest(fixture.TenantId, fixture.ProgramId));
        Assert.DoesNotContain(digest.Overdue.Concat(digest.DueSoon),
            item => item.WorkItemId == review.WorkItemId);
    }

    [Fact]
    public async Task ShouldKeepEscalationGivenReassignment()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync(backup: new OperatingHolder("member", fixture.BackupMemberId));
        var item = (await fixture.AsAsync(fixture.OwnerUserId,
                new ListWork(fixture.TenantId, fixture.ProgramId))).Items
            .First(work => work.DueOn >= fixture.Today);
        await fixture.AsAsync(fixture.OwnerUserId, new EscalateWorkItem(fixture.TenantId,
            fixture.ProgramId, item.WorkItemId, 0, "Blocked."));

        // Act
        var reassigned = await fixture.AsAsync(fixture.LeadUserId, new AssignWorkItem(
            fixture.TenantId, fixture.ProgramId, item.WorkItemId, 1, fixture.BackupMemberId));

        // Assert
        Assert.True(reassigned.Item.Escalated);
        Assert.Equal("member", reassigned.Item.EscalatedBy);
    }
}
