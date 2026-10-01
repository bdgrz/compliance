using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Operations;

public sealed class ControlOccurrenceTests
{
    [Fact]
    public async Task ShouldRecordExactVersionAndUnresolvedSupportGivenCompleteAttestation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.PlanAsync();
        var missed = (await fixture.OccurrencesAsync("missed"))[0];

        // Act
        var attested = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(missed));

        // Assert
        Assert.Equal("submitted", attested.State);
        Assert.Equal(2, attested.Revision);
        var attestation = Assert.Single(attested.Attestations);
        Assert.Equal(fixture.ControlVersionId, attestation.ControlVersionId);
        Assert.Equal(plan.PlanVersionId, attestation.PlanVersionId);
        Assert.Equal(plan.ExpectedEvidence, attestation.ExpectedEvidence);
        Assert.Equal(missed.PeriodStart, attestation.CoveredFrom);
        Assert.Equal(missed.PeriodEnd, attestation.CoveredUntil);
        Assert.All(attestation.Evidence, evidence => Assert.Equal("unresolved", evidence.Resolution));
        Assert.Equal(new OperatingHolder("member", fixture.OwnerMemberId), attestation.PerformedBy);
        Assert.Equal(fixture.OwnerMemberId, attestation.RecorderMemberId);
    }

    [Fact]
    public async Task ShouldRejectCompletionGivenMissingSupportOrRationale()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var missed = (await fixture.OccurrencesAsync("missed"))[0];

        await fixture.Scenario(fixture.OwnerUserId)
            // Act
            .When(fixture.Attest(missed, evidence: [new(0, "external", "https://x.example")]))
            // Assert
            .ExpectFailure(RequestErrorKind.Validation);
        await fixture.Scenario(fixture.OwnerUserId)
            .When(fixture.Attest(missed, "skipped", evidence: []))
            .ExpectFailure(RequestErrorKind.Validation);
        await fixture.Scenario(fixture.OwnerUserId)
            .When(fixture.Attest(missed, "partly"))
            .ExpectFailure(RequestErrorKind.Validation);
    }

    [Fact]
    public async Task ShouldRejectAttestationGivenMemberWithoutOperatingResponsibility()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var missed = (await fixture.OccurrencesAsync("missed"))[0];

        await fixture.Scenario(fixture.OutsiderUserId)
            // Act
            .When(fixture.Attest(missed))
            // Assert
            .ExpectFailure(RequestErrorKind.Forbidden);
        await fixture.Scenario(fixture.LeadUserId)
            .When(fixture.Attest(missed))
            .ExpectFailure(RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldRejectSecondAttestationGivenConcurrentSubmission()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync(backup: new OperatingHolder("member", fixture.BackupMemberId));
        var missed = (await fixture.OccurrencesAsync("missed"))[0];
        await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(missed));

        await fixture.Scenario(fixture.BackupUserId)
            // Act
            .When(fixture.Attest(missed))
            // Assert
            .ExpectFailure(RequestErrorKind.Conflict);
    }

    [Fact]
    public async Task ShouldAttributeRecorderAndPerformerGivenOffProductPersonPerformance()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync(owner: new OperatingHolder("person", fixture.PersonId));
        var missed = (await fixture.OccurrencesAsync("missed"))[0];

        // Act
        var attested = await fixture.AsAsync(fixture.LeadUserId,
            fixture.Attest(missed, personId: fixture.PersonId));

        // Assert
        var attestation = Assert.Single(attested.Attestations);
        Assert.Equal(new OperatingHolder("person", fixture.PersonId), attestation.PerformedBy);
        Assert.Equal(fixture.LeadMemberId, attestation.RecorderMemberId);
        await fixture.Scenario(fixture.LeadUserId)
            .When(fixture.Review(attested, "approved"))
            .ExpectFailure(RequestErrorKind.Forbidden);
        await fixture.Scenario(fixture.OutsiderUserId)
            .When(fixture.Attest(missed, personId: fixture.PersonId))
            .ExpectFailure(RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldSeparatePerformanceFromIndependentReviewGivenSubmission()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var missed = (await fixture.OccurrencesAsync("missed"))[0];
        var submitted = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(missed));
        await fixture.Scenario(fixture.OwnerUserId).When(fixture.Review(submitted, "approved"))
            .ExpectFailure(RequestErrorKind.Forbidden);
        await fixture.Scenario(fixture.OutsiderUserId).When(fixture.Review(submitted, "approved"))
            .ExpectFailure(RequestErrorKind.Forbidden);

        // Act
        var reviewed = await fixture.AsAsync(fixture.ReviewerUserId,
            fixture.Review(submitted, "approved"));

        // Assert
        Assert.Equal("approved", reviewed.State);
        var review = Assert.Single(reviewed.Reviews);
        Assert.Equal(submitted.Attestations[0].AttestationId, review.AttestationId);
        Assert.Equal(fixture.ReviewerMemberId, review.ReviewerMemberId);
        var reviewerWork = await fixture.AsAsync(fixture.ReviewerUserId,
            new ListMyControlWork(fixture.TenantId, fixture.ProgramId));
        Assert.DoesNotContain(reviewerWork.Items, item => item.SourceId == submitted.OccurrenceId);
    }

    [Fact]
    public async Task ShouldAllowWaivedSelfReviewGivenExactAttestationScope()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync(owner: new OperatingHolder("person", fixture.PersonId));
        var missed = (await fixture.OccurrencesAsync("missed"))[0];
        var submitted = await fixture.AsAsync(fixture.LeadUserId,
            fixture.Attest(missed, personId: fixture.PersonId));
        var attestation = submitted.Attestations[0];
        var waiverId = await fixture.ApprovedWaiverAsync(new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.ControlOccurrence, submitted.OccurrenceId,
            attestation.AttestationId, attestation.Version, SeparationOfDutiesActions.Review),
            fixture.LeadMemberId);

        // Act
        var reviewed = await fixture.AsAsync(fixture.LeadUserId,
            fixture.Review(submitted, "approved", waiverId: waiverId));

        // Assert
        Assert.Equal(waiverId, reviewed.Reviews[0].SeparationOfDutiesWaiverId);
    }

    [Fact]
    public async Task ShouldKeepCorrectionHistoryGivenReturnedAttestation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var missed = (await fixture.OccurrencesAsync("missed"))[0];
        var submitted = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(missed));
        var returned = await fixture.AsAsync(fixture.ReviewerUserId,
            fixture.Review(submitted, "returned"));

        // Act
        var corrected = await fixture.AsAsync(fixture.OwnerUserId, new CorrectControlAttestation(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, returned.OccurrenceId,
            returned.Revision, "complete", DateTimeOffset.UtcNow.AddMinutes(-1), null, null,
            "Added the missing ticket.", null, OperationsFixture.FullSupport,
            "Reviewer found the removal ticket missing."));

        // Assert
        Assert.Equal("submitted", corrected.State);
        Assert.Equal(2, corrected.Attestations.Count);
        Assert.Equal(corrected.Attestations[0].AttestationId,
            corrected.Attestations[1].SupersedesAttestationId);
        Assert.Equal("Reviewer found the removal ticket missing.",
            corrected.Attestations[1].CorrectionReason);
        Assert.Equal("returned", Assert.Single(corrected.Reviews).Outcome);
        await fixture.Scenario(fixture.ReviewerUserId)
            .When(fixture.Review(corrected, "approved") with
            {
                AttestationId = corrected.Attestations[0].AttestationId,
            })
            .ExpectFailure(RequestErrorKind.Conflict);
    }

    [Fact]
    public async Task ShouldRecordRequestedActionsGivenActionRequestedReview()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var missed = (await fixture.OccurrencesAsync("missed"))[0];
        var submitted = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(missed));
        await fixture.Scenario(fixture.ReviewerUserId)
            .When(fixture.Review(submitted, "action_requested"))
            .ExpectFailure(RequestErrorKind.Validation);

        // Act
        var reviewed = await fixture.AsAsync(fixture.ReviewerUserId,
            fixture.Review(submitted, "action_requested", ["Automate leaver removal."]));

        // Assert
        Assert.Equal("action_requested", reviewed.State);
        Assert.Equal(["Automate leaver removal."], reviewed.Reviews[0].RequestedActions);
    }

    [Fact]
    public async Task ShouldOpenEventDrivenOccurrenceGivenTriggerAndRejectRecurringControl()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync(cadence: new ControlCadence("event_driven",
            Trigger: "a production change ships", DueWithinDays: 2));

        // Act
        var opened = await fixture.AsAsync(fixture.OwnerUserId, new OpenControlOccurrence(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, "Release 4.2",
            fixture.Today));

        // Assert
        Assert.Equal("event_driven", opened.Kind);
        Assert.Equal("open", opened.State);
        Assert.Equal(fixture.Today.AddDays(2), opened.DueOn);
        Assert.Equal(opened.OccurrenceId, Assert.Single(await fixture.OccurrencesAsync()).OccurrenceId);
        await fixture.Scenario(fixture.OutsiderUserId).When(new OpenControlOccurrence(
                fixture.TenantId, fixture.ProgramId, fixture.ControlId, "Release 4.3",
                fixture.Today))
            .ExpectFailure(RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldRejectOpeningGivenRecurringCadence()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();

        await fixture.Scenario(fixture.OwnerUserId)
            // Act
            .When(new OpenControlOccurrence(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, "Extra run", fixture.Today))
            // Assert
            .ExpectFailure(RequestErrorKind.Conflict);
    }
}
