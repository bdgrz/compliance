using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Operations;

public sealed class ControlOperatingPlanTests
{
    [Fact]
    public async Task ShouldPutPlanIntoEffectGivenIndependentApproval()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var proposed = await fixture.ProposeAsync(0,
            backup: new OperatingHolder("member", fixture.BackupMemberId));

        // Act
        var approved = await fixture.AsAsync(fixture.ApproverUserId,
            new ApproveControlOperatingPlan(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, proposed.Revision, proposed.PlanVersionId, "Looks right."));

        // Assert
        Assert.Equal("pending_approval", proposed.Status);
        Assert.Equal("approved", approved.Status);
        Assert.Equal(fixture.ControlVersionId, approved.ControlVersionId);
        Assert.Equal(["Signed review record", "Ticket for each removal"], approved.ExpectedEvidence);
        Assert.StartsWith("Every month starting", approved.CadenceDescription, StringComparison.Ordinal);
        Assert.Equal(fixture.ApproverMemberId.ToString(), approved.ApprovedBy!.Id);
        var plans = await fixture.GetPlansAsync();
        Assert.Equal(approved.PlanVersionId, plans.Current!.PlanVersionId);
        Assert.Null(plans.Pending);
    }

    [Fact]
    public async Task ShouldRejectApprovalGivenProposerApprovesOwnPlan()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var proposed = await fixture.ProposeAsync(0);

        await fixture.Scenario(fixture.LeadUserId)
            // Act
            .When(new ApproveControlOperatingPlan(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, proposed.Revision, proposed.PlanVersionId, "Mine."))
            // Assert
            .ExpectFailure(RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldRejectProposalGivenContributorWithoutProgramManagement()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();

        await fixture.Scenario(fixture.OwnerUserId)
            // Act
            .When(fixture.Propose(0))
            // Assert
            .ExpectFailure(RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldRequireWaiverGivenReviewerAlsoHoldsOwnership()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var selfReview = fixture.Propose(0, reviewer: fixture.OwnerMemberId);
        await fixture.Scenario(fixture.LeadUserId).When(selfReview)
            .ExpectFailure(RequestErrorKind.Conflict);
        var waiverId = await fixture.ApprovedWaiverAsync(new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.ControlOperatingPlan, fixture.ControlId,
            fixture.ControlVersionId, 1, SeparationOfDutiesActions.Review), fixture.OwnerMemberId);

        // Act
        var proposed = await fixture.AsAsync(fixture.LeadUserId,
            selfReview with { SeparationOfDutiesWaiverId = waiverId });

        // Assert
        Assert.Equal(waiverId, proposed.ProposalSeparationOfDutiesWaiverId);
    }

    [Fact]
    public async Task ShouldRejectProposalGivenStaleRevisionOrPendingPlan()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.ProposeAsync(0);

        await fixture.Scenario(fixture.LeadUserId)
            // Act
            .When(fixture.Propose(0))
            // Assert
            .ExpectFailure(RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.LeadUserId).When(fixture.Propose(1))
            .ExpectFailure(RequestErrorKind.Conflict);
    }

    [Fact]
    public async Task ShouldRejectProposalGivenInactiveHolderOrInvalidCadence()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.SuspendAsync(fixture.BackupUserId);

        await fixture.Scenario(fixture.LeadUserId)
            // Act
            .When(fixture.Propose(0, backup: new OperatingHolder("member", fixture.BackupMemberId)))
            // Assert
            .ExpectFailure(RequestErrorKind.Validation);
        await fixture.Scenario(fixture.LeadUserId)
            .When(fixture.Propose(0, cadence: new ControlCadence("recurring", "hourly",
                fixture.ControlEffectiveFrom, 1)))
            .ExpectFailure(RequestErrorKind.Validation);
        await fixture.Scenario(fixture.LeadUserId)
            .When(fixture.Propose(0, owner: new OperatingHolder("person", Uuid.CreateVersion4())))
            .ExpectFailure(RequestErrorKind.Validation);
    }

    [Fact]
    public async Task ShouldShowBlockersGivenActiveControlWithoutPlanThenMissedOccurrences()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var before = await fixture.BlockersAsync();

        // Act
        await fixture.PlanAsync();
        var after = await fixture.BlockersAsync();

        // Assert
        Assert.Equal("missing_plan", Assert.Single(before).Kind);
        Assert.DoesNotContain(after, blocker => blocker.Kind == "missing_plan");
        Assert.Contains(after, blocker => blocker.Kind == "missed_occurrence" &&
            blocker.OccurrenceId is not null);
    }

    [Fact]
    public async Task ShouldShowOwnerInactiveBlockerGivenDeprovisionedOwner()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();

        // Act
        await fixture.SuspendAsync(fixture.OwnerUserId);

        // Assert
        Assert.Contains(await fixture.BlockersAsync(), blocker => blocker.Kind == "owner_inactive");
    }

    [Fact]
    public async Task ShouldShowCurrentAndUpcomingWorkGivenOwnerThroughTeam()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.AddToTeamAsync(fixture.OwnerUserId);
        await fixture.PlanAsync(owner: new OperatingHolder("team", fixture.TeamId));

        // Act
        var work = await fixture.AsAsync(fixture.OwnerUserId, new ListMyControlWork(
            fixture.TenantId, fixture.ProgramId, 40));
        await fixture.RemoveFromTeamAsync(fixture.OwnerUserId);
        var afterRemoval = await fixture.AsAsync(fixture.OwnerUserId, new ListMyControlWork(
            fixture.TenantId, fixture.ProgramId, 40));
        var reviewer = await fixture.AsAsync(fixture.ReviewerUserId, new ListMyControlWork(
            fixture.TenantId, fixture.ProgramId));

        // Assert
        var responsibility = Assert.Single(work.Responsibilities);
        Assert.Equal("owner", responsibility.Role);
        Assert.Equal("team", responsibility.Holder.Kind);
        Assert.Contains(work.Items, item => item.Kind == "control_occurrence" && item.Overdue);
        Assert.Contains(work.Items, item => item.Kind == "control_occurrence" && !item.Overdue);
        Assert.Equal(work.Items.OrderBy(item => item.DueOn).Select(item => item.SourceId),
            work.Items.Select(item => item.SourceId));
        Assert.Empty(afterRemoval.Responsibilities);
        Assert.Equal("reviewer", Assert.Single(reviewer.Responsibilities).Role);
    }

    [Fact]
    public async Task ShouldReassignOpenWorkExplicitlyGivenOwnershipChange()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var adHoc = new ControlCadence("ad_hoc", DueWithinDays: 10);
        await fixture.PlanAsync(cadence: adHoc);
        var performed = await fixture.AsAsync(fixture.OwnerUserId, new OpenControlOccurrence(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, "New hire batch",
            fixture.Today.AddDays(-2)));
        performed = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(performed));
        var open = await fixture.AsAsync(fixture.OwnerUserId, new OpenControlOccurrence(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, "Contractor batch",
            fixture.Today.AddDays(-1)));
        var preview = await fixture.AsAsync(fixture.LeadUserId, new PreviewControlOperatingPlan(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, fixture.ControlVersionId,
            new OperatingHolder("member", fixture.BackupMemberId), null, fixture.ReviewerMemberId,
            adHoc, fixture.Today));

        // Act
        var plan = await fixture.PlanAsync(owner: new OperatingHolder("member",
            fixture.BackupMemberId), cadence: adHoc, effectiveFrom: fixture.Today);

        // Assert
        Assert.Equal(open.OccurrenceId, Assert.Single(preview.Reassignments).OccurrenceId);
        var reassignment = Assert.Single(plan.Reassignments!);
        Assert.Equal(open.OccurrenceId, reassignment.OccurrenceId);
        Assert.Equal(fixture.OwnerHolder, reassignment.From);
        var moved = await fixture.GetOccurrenceAsync(open.OccurrenceId);
        Assert.Equal(fixture.BackupMemberId, moved.Assignee.Id);
        var history = await fixture.GetOccurrenceAsync(performed.OccurrenceId);
        Assert.Equal(fixture.OwnerMemberId, history.Attestations[0].PerformedBy.Id);
        var plans = await fixture.GetPlansAsync();
        Assert.Equal("superseded", plans.History[0].Status);
        Assert.Equal(fixture.Today, plans.History[0].EffectiveUntil);
        await fixture.Scenario(fixture.OwnerUserId).When(fixture.Attest(moved))
            .ExpectFailure(RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldStopFutureWorkAndKeepHistoryGivenRetiredControl()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var first = (await fixture.OccurrencesAsync())[0];
        await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(first));

        // Act
        await fixture.RetireControlAsync(fixture.Today.AddDays(-20));

        // Assert
        var occurrences = await fixture.OccurrencesAsync();
        Assert.All(occurrences, occurrence =>
            Assert.True(occurrence.PeriodStart < fixture.Today.AddDays(-20)));
        Assert.Single((await fixture.GetOccurrenceAsync(first.OccurrenceId)).Attestations);
        Assert.Empty(await fixture.BlockersAsync());
        await fixture.Scenario(fixture.LeadUserId).When(fixture.Propose(2))
            .ExpectFailure(RequestErrorKind.Conflict);
    }

    [Fact]
    public async Task ShouldNotDiscloseGivenControlFromAnotherTenantOrProgram()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();

        await fixture.Scenario(fixture.LeadUserId)
            // Act
            .When(new GetControlOperatingPlan(Uuid.CreateVersion4(), fixture.ProgramId,
                fixture.ControlId))
            // Assert
            .ExpectFailure(RequestErrorKind.NotFound);
        await fixture.Scenario(fixture.LeadUserId)
            .When(new GetControlOperatingPlan(fixture.TenantId, Uuid.CreateVersion4(),
                fixture.ControlId))
            .ExpectFailure(RequestErrorKind.NotFound);
    }

    [Fact]
    public void ShouldDescribeCadenceInUserLanguageGivenEachKind()
    {
        // Arrange
        var quarterly = new ControlCadence("recurring", "quarterly", new DateOnly(2026, 1, 1), 15);
        var triggered = new ControlCadence("event_driven", Trigger: "a production change ships",
            DueWithinDays: 2);

        // Act
        var periods = ControlCadenceSchedule.Periods(quarterly, new DateOnly(2026, 1, 1), null,
            new DateOnly(2026, 12, 31)).ToArray();

        // Assert
        Assert.Equal("Every quarter starting 2026-01-01, due 15 days after the period ends",
            ControlCadenceSchedule.Describe(quarterly));
        Assert.Equal("Whenever a production change ships, due 2 days after the event",
            ControlCadenceSchedule.Describe(triggered));
        Assert.Equal("As needed (ad hoc)", ControlCadenceSchedule.Describe(new ControlCadence("ad_hoc")));
        Assert.Equal(4, periods.Length);
        Assert.Equal(new DateOnly(2026, 3, 31), periods[0].End);
        Assert.Equal(new DateOnly(2026, 4, 15), periods[0].DueOn);
    }
}
