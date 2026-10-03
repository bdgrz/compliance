using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskGovernanceLedgerTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid RiskId = Uuid.CreateVersion4();
    static readonly Uuid ControlId = Uuid.CreateVersion4();
    static readonly Uuid ControlVersionId = Uuid.CreateVersion4();
    static readonly Uuid ProposerId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldAcceptControlTreatmentGivenIndependentReviewer()
    {
        // Arrange
        var ledger = New();
        var treatmentId = Propose(ledger, 0);

        // Act
        var review = ledger.ReviewControlTreatment(RiskId, treatmentId, 1, Uuid.CreateVersion4(),
            "accept", "The control addresses the scenario.", ReviewerId,
            ActorReference.ForMember(ReviewerId, "Reviewer"), Now.AddMinutes(1));

        // Assert
        Assert.Null(review);
        Assert.True(ledger.HasAcceptedControlTreatment(RiskId));
        var treatment = ledger.FindTreatment(RiskId, treatmentId)!;
        Assert.Equal("accepted", treatment.Status);
        Assert.Equal(ControlVersionId, treatment.ControlVersionId);
        Assert.Equal(ActorReference.ForMember(ReviewerId, "Reviewer"), treatment.ReviewedBy);
        Assert.Equal(2, ledger.RevisionOf(RiskId));
        Assert.Single(ledger.TreatmentsForControl(ControlId));
    }

    [Fact]
    public void ShouldRejectSelfReviewGivenProposerWithoutWaiver()
    {
        // Arrange
        var ledger = New();
        var treatmentId = Propose(ledger, 0);
        var waiver = Waiver(new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.RiskControlTreatment, treatmentId, ControlVersionId, 1,
            SeparationOfDutiesActions.Review), ProposerId);
        var wrongRevision = Waiver(new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.RiskControlTreatment, treatmentId, ControlVersionId, 2,
            SeparationOfDutiesActions.Review), ProposerId);

        // Act
        var self = ledger.ReviewControlTreatment(RiskId, treatmentId, 1, Uuid.CreateVersion4(),
            "accept", "Mine", ProposerId, ActorReference.ForMember(ProposerId, "Proposer"),
            Now.AddHours(1));
        var mismatched = ledger.ReviewControlTreatment(RiskId, treatmentId, 1,
            Uuid.CreateVersion4(), "accept", "Mine", ProposerId,
            ActorReference.ForMember(ProposerId, "Proposer"), Now.AddHours(1), wrongRevision);
        var waived = ledger.ReviewControlTreatment(RiskId, treatmentId, 1, Uuid.CreateVersion4(),
            "accept", "Waived", ProposerId, ActorReference.ForMember(ProposerId, "Proposer"),
            Now.AddHours(1), waiver);

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited, self!.Code);
        Assert.Equal(CommandFailureCode.ActorProhibited, mismatched!.Code);
        Assert.Null(waived);
        Assert.Equal(waiver.Id, ledger.FindTreatment(RiskId, treatmentId)!
            .SeparationOfDutiesWaiverId);
    }

    [Fact]
    public void ShouldRejectProposalGivenNonMitigateTreatmentStaleRevisionOrPendingDuplicate()
    {
        // Arrange
        var ledger = New();
        _ = Propose(ledger, 0);

        // Act
        var accept = ledger.ProposeControlTreatment(RiskId, 1, Uuid.CreateVersion4(), "accept",
            ControlId, ControlVersionId, "Wrong", ProposerId, Proposer(), Now);
        var stale = ledger.ProposeControlTreatment(RiskId, 0, Uuid.CreateVersion4(), "mitigate",
            ControlId, ControlVersionId, "Stale", ProposerId, Proposer(), Now);
        var pending = ledger.ProposeControlTreatment(RiskId, 1, Uuid.CreateVersion4(),
            "mitigate", ControlId, ControlVersionId, "Again", ProposerId, Proposer(), Now);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, accept!.Code);
        Assert.Equal(CommandFailureCode.VersionConflict, stale!.Code);
        Assert.Equal(CommandFailureCode.StateConflict, pending!.Code);
        Assert.Equal(1, ledger.RevisionOf(RiskId));
    }

    [Fact]
    public void ShouldSupersedeEarlierAssertionGivenAcceptedSuccessorControlVersion()
    {
        // Arrange
        var ledger = New();
        var first = Propose(ledger, 0);
        Assert.Null(Review(ledger, first, 1, "accept"));
        var successorVersionId = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        Assert.Null(ledger.ProposeControlTreatment(RiskId, 2, second, "mitigate", ControlId,
            successorVersionId, "Successor treats the risk.", ProposerId, Proposer(), Now));

        // Act
        var review = Review(ledger, second, 3, "accept");
        var retire = ledger.RetireControlTreatment(RiskId, second, 4, "Control retired.",
            Proposer(), Now.AddDays(1));

        // Assert
        Assert.Null(review);
        Assert.Null(retire);
        Assert.Equal("superseded", ledger.FindTreatment(RiskId, first)!.Status);
        Assert.Equal("retired", ledger.FindTreatment(RiskId, second)!.Status);
        Assert.False(ledger.HasAcceptedControlTreatment(RiskId));
        Assert.Empty(ledger.TreatmentsForControl(ControlId));
    }

    [Fact]
    public void ShouldRaiseTriggerOnceAndResolveGivenLaterInherentAssessment()
    {
        // Arrange
        var ledger = New();
        var methodVersionId = Uuid.CreateVersion4().ToString();

        // Act
        ledger.RaiseTrigger(RiskId, "method_changed", methodVersionId, Now);
        ledger.RaiseTrigger(RiskId, "method_changed", methodVersionId, Now.AddMinutes(5));
        var open = ledger.OpenTriggers(RiskId, [Assessment(Now.AddMinutes(-1))]);
        var resolved = ledger.OpenTriggers(RiskId, [Assessment(Now.AddMinutes(1))]);

        // Assert
        var trigger = Assert.Single(open);
        Assert.Equal("method_changed", trigger.TriggerKind);
        Assert.Equal(RiskGovernanceLedger.TriggerIdFor(ProgramId, RiskId, "method_changed",
            methodVersionId), trigger.TriggerId);
        Assert.Empty(resolved);
        Assert.Equal(1, ledger.RevisionOf(RiskId));
        Assert.Equal("resolved", Assert.Single(ledger.View(RiskId,
            [Assessment(Now.AddMinutes(1))], new DateOnly(2026, 10, 1)).ReassessmentTriggers)
            .Status);
    }

    [Fact]
    public void ShouldRecordPersonOwnerGivenCorrelatedMember()
    {
        // Arrange
        var ledger = New();
        var personId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();

        // Act
        var assigned = ledger.AssignOwner(RiskId, 0, personId, memberId, "Owns the service.",
            Proposer(), Now);
        var repeat = ledger.AssignOwner(RiskId, 1, personId, memberId, "Again", Proposer(), Now);
        var empty = ledger.AssignOwner(RiskId, 1, Uuid.Empty, null, "None", Proposer(), Now);

        // Assert
        Assert.Null(assigned);
        Assert.Equal(CommandFailureCode.StateConflict, repeat!.Code);
        Assert.Equal(CommandFailureCode.InvalidContent, empty!.Code);
        var owner = ledger.OwnerOf(RiskId)!;
        Assert.Equal(personId, owner.PersonId);
        Assert.Equal(memberId, owner.CorrelatedMemberId);
        var pending = new AggregateScenario<RiskGovernanceLedger>(ledger).PendingEvents;
        Assert.IsType<RiskOwnerAssigned>(Assert.Single(pending));
    }

    [Fact]
    public void ShouldKeepActionIncompleteGivenSubmissionUntilIndependentReviewAccepts()
    {
        // Arrange
        var ledger = New();
        var actionId = AddAction(ledger, 0);
        var evidenceId = Uuid.CreateVersion4();
        var fulfilled = new HashSet<Uuid> { evidenceId };

        // Act
        var noEvidence = ledger.SubmitActionCompletion(RiskId, actionId, 1, Uuid.CreateVersion4(),
            "Done.", [], new HashSet<Uuid>(), ProposerId, Proposer(), Now.AddDays(1));
        var unfulfilled = ledger.SubmitActionCompletion(RiskId, actionId, 1,
            Uuid.CreateVersion4(), "Done.", [evidenceId], new HashSet<Uuid>(), ProposerId,
            Proposer(), Now.AddDays(1));
        var submissionId = Uuid.CreateVersion4();
        var submitted = ledger.SubmitActionCompletion(RiskId, actionId, 1, submissionId, "Done.",
            [evidenceId], fulfilled, ProposerId, Proposer(), Now.AddDays(1));
        var pending = ledger.View(RiskId, [], DateOnly.FromDateTime(Now.UtcDateTime));
        var self = ledger.ReviewActionCompletion(RiskId, actionId, 2, Uuid.CreateVersion4(),
            "accept", "Mine.", ProposerId, Proposer(), Now.AddDays(2), fulfilled);
        var lostEvidence = ledger.ReviewActionCompletion(RiskId, actionId, 2,
            Uuid.CreateVersion4(), "accept", "Gone.", ReviewerId,
            ActorReference.ForMember(ReviewerId, "Reviewer"), Now.AddDays(2),
            new HashSet<Uuid>());
        var accepted = ledger.ReviewActionCompletion(RiskId, actionId, 2, Uuid.CreateVersion4(),
            "accept", "Evidence shows it.", ReviewerId,
            ActorReference.ForMember(ReviewerId, "Reviewer"), Now.AddDays(2), fulfilled);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, noEvidence!.Code);
        Assert.Equal(CommandFailureCode.StateConflict, unfulfilled!.Code);
        Assert.Null(submitted);
        var pendingAction = Assert.Single(pending.TreatmentActions);
        Assert.Equal("completion_submitted", pendingAction.Status);
        Assert.Equal("in_progress", pending.TreatmentActionStatus);
        Assert.Equal(CommandFailureCode.ActorProhibited, self!.Code);
        Assert.Equal(CommandFailureCode.StateConflict, lostEvidence!.Code);
        Assert.Null(accepted);
        var done = ledger.View(RiskId, [], DateOnly.FromDateTime(Now.UtcDateTime));
        Assert.Equal("completed", Assert.Single(done.TreatmentActions).Status);
        Assert.Equal("completed", done.TreatmentActionStatus);
        Assert.Equal(3, ledger.RevisionOf(RiskId));
    }

    [Fact]
    public void ShouldReviseOpenTreatmentActionGivenValidEdit()
    {
        // Arrange
        var ledger = New();
        var actionId = AddAction(ledger, 0);
        var requestId = Uuid.CreateVersion4();
        var evidenceRequestId = Uuid.CreateVersion4();
        var revisedBy = ActorReference.ForMember(ReviewerId, "Reviewer");
        var revisedAt = Now.AddDays(1);

        // Act
        var failure = ledger.ReviseTreatmentAction(RiskId, actionId, 1, requestId,
            "Enforce phishing-resistant MFA", "Every administrator uses a hardware key.",
            "Identity provider policy export.", new DateOnly(2026, 12, 1), ReviewerId,
            [evidenceRequestId], revisedBy, revisedAt);

        // Assert
        Assert.Null(failure);
        var action = ledger.FindAction(RiskId, actionId)!;
        Assert.Equal("Enforce phishing-resistant MFA", action.Title);
        Assert.Equal("Every administrator uses a hardware key.", action.TargetState);
        Assert.Equal("Identity provider policy export.", action.ExpectedEvidence);
        Assert.Equal(new DateOnly(2026, 12, 1), action.DueOn);
        Assert.Equal(ReviewerId, action.AccountableMemberId);
        Assert.Equal([evidenceRequestId], action.EvidenceRequestIds);
        Assert.Equal("open", action.Status);
        Assert.Equal(Proposer(), action.CreatedBy);
        Assert.Equal(Now, action.CreatedAt);
        Assert.Equal(2, ledger.RevisionOf(RiskId));
        var revised = Assert.IsType<RiskTreatmentActionRevised>(
            new AggregateScenario<RiskGovernanceLedger>(ledger).PendingEvents.Single(ev =>
                ev is RiskTreatmentActionRevised));
        Assert.Equal(requestId, revised.RequestId);
        Assert.Equal(revisedBy, revised.RevisedBy);
        Assert.Equal(revisedAt, revised.RevisedAt);
    }

    [Fact]
    public void ShouldReplayTreatmentActionEditAndRejectChangedContentGivenRequestId()
    {
        // Arrange
        var ledger = New();
        var actionId = AddAction(ledger, 0);
        var requestId = Uuid.CreateVersion4();

        // Act
        var first = ledger.ReviseTreatmentAction(RiskId, actionId, 1, requestId,
            "Updated action", "Updated state", "Updated evidence", new DateOnly(2026, 12, 1),
            ReviewerId, [], ActorReference.ForMember(ReviewerId, "Reviewer"), Now.AddDays(1));
        var replay = ledger.ReviseTreatmentAction(RiskId, actionId, 1, requestId,
            "Updated action", "Updated state", "Updated evidence", new DateOnly(2026, 12, 1),
            ReviewerId, [], ActorReference.ForMember(ReviewerId, "Reviewer"), Now.AddDays(2));
        var changedReplay = ledger.ReviseTreatmentAction(RiskId, actionId, 1, requestId,
            "Changed action", "Updated state", "Updated evidence", new DateOnly(2026, 12, 1),
            ReviewerId, [], ActorReference.ForMember(ReviewerId, "Reviewer"), Now.AddDays(2));

        // Assert
        Assert.Null(first);
        Assert.Null(replay);
        Assert.Equal(CommandFailureCode.StateConflict, changedReplay!.Code);
        Assert.Equal(2, ledger.RevisionOf(RiskId));
        Assert.Equal("Updated action", ledger.FindAction(RiskId, actionId)!.Title);
    }

    [Fact]
    public void ShouldRejectTreatmentActionEditGivenStaleRevisionOrSubmittedAction()
    {
        // Arrange
        var ledger = New();
        var staleActionId = AddAction(ledger, 0);
        var submittedActionId = AddAction(ledger, 1);
        var evidenceRequestId = Uuid.CreateVersion4();
        Assert.Null(ledger.SubmitActionCompletion(RiskId, submittedActionId, 2,
            Uuid.CreateVersion4(), "Completed the action.", [evidenceRequestId],
            new HashSet<Uuid> { evidenceRequestId }, ProposerId, Proposer(), Now));

        // Act
        var stale = ledger.ReviseTreatmentAction(RiskId, staleActionId, 0,
            Uuid.CreateVersion4(), "Stale edit", "State", "Evidence",
            new DateOnly(2026, 12, 1), ReviewerId, [],
            ActorReference.ForMember(ReviewerId, "Reviewer"), Now.AddDays(1));
        var submitted = ledger.ReviseTreatmentAction(RiskId, submittedActionId, 3,
            Uuid.CreateVersion4(), "Submitted edit", "State", "Evidence",
            new DateOnly(2026, 12, 1), ReviewerId, [],
            ActorReference.ForMember(ReviewerId, "Reviewer"), Now.AddDays(1));

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, stale!.Code);
        Assert.Equal(CommandFailureCode.StateConflict, submitted!.Code);
        Assert.Equal("Enforce MFA", ledger.FindAction(RiskId, staleActionId)!.Title);
        Assert.Equal("Enforce MFA", ledger.FindAction(RiskId, submittedActionId)!.Title);
        Assert.Equal(3, ledger.RevisionOf(RiskId));
    }

    [Fact]
    public void ShouldReopenActionAndReportOverdueGivenRejectedCompletionPastDueDate()
    {
        // Arrange
        var ledger = New();
        var actionId = AddAction(ledger, 0);
        var evidenceId = Uuid.CreateVersion4();
        var fulfilled = new HashSet<Uuid> { evidenceId };
        Assert.Null(ledger.SubmitActionCompletion(RiskId, actionId, 1, Uuid.CreateVersion4(),
            "Done.", [evidenceId], fulfilled, ProposerId, Proposer(), Now));

        // Act
        var rejected = ledger.ReviewActionCompletion(RiskId, actionId, 2, Uuid.CreateVersion4(),
            "reject", "Screenshot is of the wrong tenant.", ReviewerId,
            ActorReference.ForMember(ReviewerId, "Reviewer"), Now, fulfilled);
        var late = ledger.View(RiskId, [], new DateOnly(2026, 12, 1));

        // Assert
        Assert.Null(rejected);
        var action = Assert.Single(late.TreatmentActions);
        Assert.Equal("open", action.Status);
        Assert.True(action.Overdue);
        Assert.Equal("overdue", late.TreatmentActionStatus);
        Assert.Equal("reject", Assert.Single(action.Completions).ReviewOutcome);
    }

    [Fact]
    public void ShouldRejectActionGivenPastDueDateNonTreatingRiskOrStaleRevision()
    {
        // Arrange
        var ledger = New();

        // Act
        var past = ledger.AddTreatmentAction(RiskId, 0, Uuid.CreateVersion4(), "mitigate", "T",
            "State", "Evidence", new DateOnly(2026, 9, 1), ProposerId, [], Proposer(), Now);
        var accept = ledger.AddTreatmentAction(RiskId, 0, Uuid.CreateVersion4(), "accept", "T",
            "State", "Evidence", new DateOnly(2026, 11, 1), ProposerId, [], Proposer(), Now);
        var stale = ledger.AddTreatmentAction(RiskId, 5, Uuid.CreateVersion4(), "mitigate", "T",
            "State", "Evidence", new DateOnly(2026, 11, 1), ProposerId, [], Proposer(), Now);
        var blank = ledger.AddTreatmentAction(RiskId, 0, Uuid.CreateVersion4(), "mitigate", " ",
            "State", "Evidence", new DateOnly(2026, 11, 1), ProposerId, [], Proposer(), Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, past!.Code);
        Assert.Equal(CommandFailureCode.StateConflict, accept!.Code);
        Assert.Equal(CommandFailureCode.VersionConflict, stale!.Code);
        Assert.Equal(CommandFailureCode.InvalidContent, blank!.Code);
        Assert.Equal("none", ledger.View(RiskId, [], new DateOnly(2026, 10, 1))
            .TreatmentActionStatus);
    }

    static Uuid AddAction(RiskGovernanceLedger ledger, long revision)
    {
        var actionId = Uuid.CreateVersion4();
        Assert.Null(ledger.AddTreatmentAction(RiskId, revision, actionId, "mitigate",
            "Enforce MFA", "MFA required for every administrator.", "IdP policy export.",
            new DateOnly(2026, 11, 1), ProposerId, [], Proposer(), Now));
        return actionId;
    }

    static RiskGovernanceLedger New() => new(TenantId, ProgramId);

    static ActorReference Proposer() => ActorReference.ForMember(ProposerId, "Proposer");

    static Uuid Propose(RiskGovernanceLedger ledger, long revision)
    {
        var treatmentId = Uuid.CreateVersion4();
        Assert.Null(ledger.ProposeControlTreatment(RiskId, revision, treatmentId, "mitigate",
            ControlId, ControlVersionId, "Quarterly access review treats the risk.", ProposerId,
            Proposer(), Now));
        return treatmentId;
    }

    static CommandFailure? Review(RiskGovernanceLedger ledger, Uuid treatmentId, long revision,
        string outcome) => ledger.ReviewControlTreatment(RiskId, treatmentId, revision,
        Uuid.CreateVersion4(), outcome, "Reviewed", ReviewerId,
        ActorReference.ForMember(ReviewerId, "Reviewer"), Now.AddMinutes(1));

    static RiskAssessmentView Assessment(DateTimeOffset at) => new(Uuid.CreateVersion4(),
        "inherent", Uuid.CreateVersion4(), 1, 3, 3, 9, "Assessed",
        ActorReference.ForMember(ProposerId, "Proposer"), at);

    static SeparationOfDutiesWaiver Waiver(SeparationOfDutiesWaiverScope scope, Uuid beneficiary)
    {
        var waiver = new SeparationOfDutiesWaiver(TenantId, Uuid.CreateVersion4());
        Assert.Null(waiver.Record(scope, beneficiary, Uuid.CreateVersion4(), "Requester",
            "No other qualified reviewer is available.", Now, Now.AddDays(1)));
        Assert.Null(waiver.Approve(Uuid.CreateVersion4(), "Approver", Now.AddMinutes(1)));
        return waiver;
    }
}
