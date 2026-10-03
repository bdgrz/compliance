using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Holds every risk's owner, control treatment assertions, and reassessment triggers for one
///     program in one stream, so owner-based separation of duties, residual eligibility, control
///     impact, and trigger status are exact and replay is the only recovery step. Each risk keeps
///     its own governance revision for optimistic concurrency.
/// </summary>
public sealed class RiskGovernanceLedger : Aggregate
{
    public const string Accept = "accept";
    public const string Reject = "reject";
    public const string MethodChanged = "method_changed";
    public const string BoundaryChanged = "boundary_changed";
    public const string ActionOpen = "open";
    public const string ActionSubmitted = "completion_submitted";
    public const string ActionCompleted = "completed";
    public const string ActionCancelled = "cancelled";
    const int MaximumTextLength = 4000;
    const int MaximumTitleLength = 200;

    readonly Uuid _tenantId;
    readonly Dictionary<Uuid, RiskState> _risks = [];

    public RiskGovernanceLedger(Uuid tenantId, Uuid programId)
        : base(programId, new EventStreamAddress(tenantId.ToString(), "risk-governance",
            programId.ToString()))
    {
        _tenantId = tenantId;
        On<RiskOwnerAssigned>(ev =>
        {
            var risk = State(ev.RiskId);
            risk.Revision = ev.Revision;
            risk.Owner = ev.Owner;
        });
        On<RiskControlTreatmentProposed>(ev =>
        {
            var risk = State(ev.RiskId);
            risk.Revision = ev.Revision;
            risk.Treatments.Add(ev.Treatment);
            risk.Proposers[ev.Treatment.TreatmentId] = ev.ProposerMemberId;
        });
        On<RiskControlTreatmentReviewed>(ev =>
        {
            var risk = State(ev.RiskId);
            risk.Revision = ev.Revision;
            var index = risk.Treatments.FindIndex(item => item.TreatmentId == ev.TreatmentId);
            var reviewed = risk.Treatments[index];
            if (ev.Outcome == Accept)
            {
                for (var i = 0; i < risk.Treatments.Count; i++)
                    if (risk.Treatments[i] is { Status: "accepted" } previous &&
                        previous.ControlId == reviewed.ControlId)
                        risk.Treatments[i] = previous with { Status = "superseded" };
            }
            risk.Treatments[index] = reviewed with
            {
                Status = ev.Outcome == Accept ? "accepted" : "rejected",
                ReviewDecisionId = ev.DecisionId,
                ReviewedBy = ev.Reviewer,
                ReviewRationale = ev.Rationale,
                ReviewedAt = ev.ReviewedAt,
                SeparationOfDutiesWaiverId = ev.SeparationOfDutiesWaiverId,
            };
        });
        On<RiskControlTreatmentRetired>(ev =>
        {
            var risk = State(ev.RiskId);
            risk.Revision = ev.Revision;
            var index = risk.Treatments.FindIndex(item => item.TreatmentId == ev.TreatmentId);
            risk.Treatments[index] = risk.Treatments[index] with
            {
                Status = "retired",
                RetiredBy = ev.RetiredBy,
                RetirementRationale = ev.Rationale,
                RetiredAt = ev.RetiredAt,
            };
        });
        On<RiskReassessmentTriggered>(ev =>
        {
            var risk = State(ev.RiskId);
            risk.Revision = ev.Revision;
            risk.Triggers.Add(ev.Trigger);
        });
        On<RiskTreatmentActionAdded>(ev =>
        {
            var risk = State(ev.RiskId);
            risk.Revision = ev.Revision;
            risk.Actions.Add(ev.Action);
        });
        On<RiskTreatmentActionCancelled>(ev =>
        {
            var risk = State(ev.RiskId);
            risk.Revision = ev.Revision;
            var index = risk.Actions.FindIndex(item => item.ActionId == ev.ActionId);
            var action = risk.Actions[index];
            risk.Actions[index] = action with
            {
                Status = ActionCancelled,
                CancellationRationale = ev.Rationale,
                CancelledBy = ev.CancelledBy,
                CancelledAt = ev.CancelledAt,
            };
            risk.CancellationIds[ev.ActionId] = ev.CancellationId;
        });
        On<RiskTreatmentActionRevised>(ev =>
        {
            var risk = State(ev.RiskId);
            risk.Revision = ev.Revision;
            var index = risk.Actions.FindIndex(item => item.ActionId == ev.ActionId);
            var action = risk.Actions[index];
            risk.Actions[index] = action with
            {
                Title = ev.Title,
                TargetState = ev.TargetState,
                ExpectedEvidence = ev.ExpectedEvidence,
                DueOn = ev.DueOn,
                AccountableMemberId = ev.AccountableMemberId,
                EvidenceRequestIds = ev.EvidenceRequestIds,
            };
            risk.ActionEditRequests[ev.RequestId] = new ActionEditRequest(ev.RiskId,
                ev.ActionId, ev.ExpectedRevision, ev.Title, ev.TargetState,
                ev.ExpectedEvidence, ev.DueOn, ev.AccountableMemberId,
                ev.EvidenceRequestIds);
        });
        On<RiskTreatmentActionCompletionSubmitted>(ev =>
        {
            var risk = State(ev.RiskId);
            risk.Revision = ev.Revision;
            var index = risk.Actions.FindIndex(item => item.ActionId == ev.ActionId);
            var action = risk.Actions[index];
            risk.Actions[index] = action with
            {
                Status = ActionSubmitted,
                Completions =
                [
                    .. action.Completions,
                    new RiskTreatmentActionCompletionView(ev.SubmissionId, ev.Summary,
                        ev.EvidenceRequestIds, ev.SubmittedBy, ev.SubmittedAt, null, null, null,
                        null, null, null),
                ],
            };
            risk.Submitters[ev.SubmissionId] = ev.SubmitterMemberId;
        });
        On<RiskTreatmentActionCompletionReviewed>(ev =>
        {
            var risk = State(ev.RiskId);
            risk.Revision = ev.Revision;
            var index = risk.Actions.FindIndex(item => item.ActionId == ev.ActionId);
            var action = risk.Actions[index];
            var last = action.Completions[^1];
            risk.Actions[index] = action with
            {
                Status = ev.Outcome == Accept ? ActionCompleted : ActionOpen,
                Completions =
                [
                    .. action.Completions.Take(action.Completions.Count - 1),
                    last with
                    {
                        ReviewDecisionId = ev.DecisionId,
                        ReviewOutcome = ev.Outcome,
                        ReviewedBy = ev.Reviewer,
                        ReviewRationale = ev.Rationale,
                        ReviewedAt = ev.ReviewedAt,
                        SeparationOfDutiesWaiverId = ev.SeparationOfDutiesWaiverId,
                    },
                ],
            };
        });
    }

    /// <summary>A stable trigger identity, so a replayed reaction never raises it twice.</summary>
    public static Uuid TriggerIdFor(Uuid programId, Uuid riskId, string triggerKind,
        string sourceReference) => Uuid.CreateVersion5(
        Uuid.CreateVersion5(programId, "risk-reassessment-trigger"),
        riskId + "|" + triggerKind + "|" + sourceReference);

    public long RevisionOf(Uuid riskId) =>
        _risks.TryGetValue(riskId, out var risk) ? risk.Revision : 0;

    public RiskOwnerView? OwnerOf(Uuid riskId) =>
        _risks.TryGetValue(riskId, out var risk) ? risk.Owner : null;

    /// <summary>Whether an independently accepted control treatment is in force.</summary>
    public bool HasAcceptedControlTreatment(Uuid riskId) =>
        _risks.TryGetValue(riskId, out var risk) &&
        risk.Treatments.Any(static treatment => treatment.Status == "accepted");

    public RiskControlTreatmentView? FindTreatment(Uuid riskId, Uuid treatmentId) =>
        _risks.TryGetValue(riskId, out var risk)
            ? risk.Treatments.Find(item => item.TreatmentId == treatmentId)
            : null;

    /// <summary>Every proposed or accepted treatment assertion that names the control.</summary>
    public IReadOnlyList<RiskControlTreatmentView> TreatmentsForControl(Uuid controlId) =>
        [.. _risks.Values.SelectMany(static risk => risk.Treatments)
            .Where(treatment => treatment.ControlId == controlId &&
                treatment.Status is "proposed" or "accepted")];

    public RiskGovernanceView View(Uuid riskId, IReadOnlyList<RiskAssessmentView> assessments,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(assessments);
        _risks.TryGetValue(riskId, out var risk);
        var actions = (risk?.Actions ?? [])
            .Select(action => action with { Overdue = IsOverdue(action, today) }).ToArray();
        return new RiskGovernanceView(_tenantId, Id, riskId, risk?.Revision ?? 0, risk?.Owner,
            risk?.Treatments.ToArray() ?? [],
            [.. (risk?.Triggers ?? []).Select(trigger => WithStatus(trigger, assessments))],
            actions, ActionStatus(actions));
    }

    /// <summary>Every treatment action in the program, in the order it was added.</summary>
    public IReadOnlyList<RiskTreatmentActionView> Actions() =>
        [.. _risks.Values.SelectMany(static risk => risk.Actions)];

    public RiskTreatmentActionView? FindAction(Uuid riskId, Uuid actionId) =>
        _risks.TryGetValue(riskId, out var risk)
            ? risk.Actions.Find(item => item.ActionId == actionId)
            : null;

    static bool IsOverdue(RiskTreatmentActionView action, DateOnly today) =>
        action.Status is not (ActionCompleted or ActionCancelled) && action.DueOn < today;

    static string ActionStatus(RiskTreatmentActionView[] actions)
    {
        var active = actions.Where(static action => action.Status != ActionCancelled).ToArray();
        return active.Length == 0 ? "none"
        : active.All(static action => action.Status == ActionCompleted) ? "completed"
        : active.Any(static action => action.Overdue) ? "overdue"
        : "in_progress";
    }

    /// <summary>Triggers not yet answered by an inherent assessment recorded at or after them.</summary>
    public IReadOnlyList<RiskReassessmentTriggerView> OpenTriggers(Uuid riskId,
        IReadOnlyList<RiskAssessmentView> assessments)
    {
        ArgumentNullException.ThrowIfNull(assessments);
        return _risks.TryGetValue(riskId, out var risk)
            ? [.. risk.Triggers.Select(trigger => WithStatus(trigger, assessments))
                .Where(static trigger => trigger.Status == "open")]
            : [];
    }

    static RiskReassessmentTriggerView WithStatus(RiskReassessmentTriggerView trigger,
        IReadOnlyList<RiskAssessmentView> assessments) => trigger with
        {
            Status = assessments.Any(assessment => assessment.Phase == RiskEvaluation.Inherent &&
                assessment.AssessedAt >= trigger.RaisedAt)
            ? "resolved"
            : "open",
        };

    public CommandFailure? AssignOwner(Uuid riskId, long expectedRevision, Uuid personId,
        Uuid? correlatedMemberId, string rationale, ActorReference actor,
        DateTimeOffset assignedAt)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (CheckRevision(riskId, expectedRevision) is { } stale)
            return stale;
        if (personId == Uuid.Empty || InvalidText(rationale))
            return CommandFailure.InvalidContent(
                "A risk owner requires a workforce person and a rationale of at most 4000 characters.");
        if (OwnerOf(riskId) is { } current && current.PersonId == personId &&
            current.CorrelatedMemberId == correlatedMemberId)
            return CommandFailure.StateConflict("The person already owns the risk.");
        RaiseEvent(new RiskOwnerAssigned(_tenantId, Id, riskId, RevisionOf(riskId) + 1,
            new RiskOwnerView(personId, correlatedMemberId, rationale.Trim(), actor,
                assignedAt)));
        return null;
    }

    /// <param name="riskTreatmentKind">The risk's currently chosen treatment kind, if any.</param>
    public CommandFailure? ProposeControlTreatment(Uuid riskId, long expectedRevision,
        Uuid treatmentId, string? riskTreatmentKind, Uuid controlId, Uuid controlVersionId,
        string rationale, Uuid proposerMemberId, ActorReference proposer,
        DateTimeOffset proposedAt)
    {
        ArgumentNullException.ThrowIfNull(proposer);
        if (FindTreatment(riskId, treatmentId) is { } existing)
            return existing.ControlId == controlId &&
                   existing.ControlVersionId == controlVersionId &&
                   StringComparer.Ordinal.Equals(existing.Rationale, rationale?.Trim())
                ? null
                : CommandFailure.StateConflict(
                    "The treatment request was already recorded with different content.");
        if (CheckRevision(riskId, expectedRevision) is { } stale)
            return stale;
        if (treatmentId == Uuid.Empty || controlId == Uuid.Empty ||
            controlVersionId == Uuid.Empty || InvalidText(rationale))
            return CommandFailure.InvalidContent(
                "A control treatment requires an approved control version and a rationale of at most 4000 characters.");
        if (riskTreatmentKind != "mitigate")
            return CommandFailure.StateConflict(
                "Choose the mitigate treatment before asserting a control treatment.");
        var treatments = _risks.TryGetValue(riskId, out var risk) ? risk.Treatments : [];
        if (treatments.Any(item => item.ControlId == controlId && item.Status == "proposed"))
            return CommandFailure.StateConflict(
                "The control already has a pending treatment assertion for this risk.");
        if (treatments.Any(item => item.ControlId == controlId && item.Status == "accepted" &&
                item.ControlVersionId == controlVersionId))
            return CommandFailure.StateConflict(
                "An accepted treatment assertion already names this exact control version.");
        RaiseEvent(new RiskControlTreatmentProposed(_tenantId, Id, riskId,
            RevisionOf(riskId) + 1, new RiskControlTreatmentView(treatmentId, riskId, controlId,
                controlVersionId, "proposed", rationale.Trim(), proposer, proposedAt, null, null,
                null, null, null, null, null, null), proposerMemberId));
        return null;
    }

    public CommandFailure? ReviewControlTreatment(Uuid riskId, Uuid treatmentId,
        long expectedRevision, Uuid decisionId, string outcome, string rationale,
        Uuid reviewerMemberId, ActorReference reviewer, DateTimeOffset reviewedAt,
        SeparationOfDutiesWaiver? waiver = null)
    {
        ArgumentNullException.ThrowIfNull(reviewer);
        if (FindTreatment(riskId, treatmentId) is not { } treatment)
            return CommandFailure.MissingRecord("The risk control treatment was not found.");
        if (treatment.Status != "proposed")
            return CommandFailure.StateConflict(
                "The control treatment has no pending assertion to review.");
        if (CheckRevision(riskId, expectedRevision) is { } stale)
            return stale;
        if (waiver is not null && (waiver.TenantId != _tenantId || !waiver.Allows(
                new SeparationOfDutiesWaiverScope(
                    SeparationOfDutiesRecordTypes.RiskControlTreatment, treatmentId,
                    treatment.ControlVersionId, expectedRevision,
                    SeparationOfDutiesActions.Review), reviewerMemberId, reviewedAt)))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this member and treatment revision.");
        if (_risks[riskId].Proposers[treatmentId] == reviewerMemberId && waiver is null)
            return CommandFailure.ActorProhibited(
                "A control treatment proposer cannot review their own assertion.");
        if (decisionId == Uuid.Empty || outcome is not (Accept or Reject) ||
            InvalidText(rationale))
            return CommandFailure.InvalidContent(
                "A treatment review requires an outcome of accept or reject and a rationale of at most 4000 characters.");
        RaiseEvent(new RiskControlTreatmentReviewed(_tenantId, Id, riskId,
            RevisionOf(riskId) + 1, treatmentId, decisionId, outcome, rationale.Trim(),
            reviewer, reviewedAt, waiver?.Id));
        return null;
    }

    public CommandFailure? RetireControlTreatment(Uuid riskId, Uuid treatmentId,
        long expectedRevision, string rationale, ActorReference actor, DateTimeOffset retiredAt)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (FindTreatment(riskId, treatmentId) is not { } treatment)
            return CommandFailure.MissingRecord("The risk control treatment was not found.");
        if (CheckRevision(riskId, expectedRevision) is { } stale)
            return stale;
        if (treatment.Status != "accepted")
            return CommandFailure.StateConflict(
                "Only an accepted control treatment assertion can be retired.");
        if (InvalidText(rationale))
            return CommandFailure.InvalidContent(
                "Retiring a control treatment requires a rationale of at most 4000 characters.");
        RaiseEvent(new RiskControlTreatmentRetired(_tenantId, Id, riskId,
            RevisionOf(riskId) + 1, treatmentId, rationale.Trim(), actor, retiredAt));
        return null;
    }

    /// <param name="riskTreatmentKind">The risk's currently chosen treatment kind, if any.</param>
    public CommandFailure? AddTreatmentAction(Uuid riskId, long expectedRevision, Uuid actionId,
        string? riskTreatmentKind, string title, string targetState, string expectedEvidence,
        DateOnly dueOn, Uuid accountableMemberId, IReadOnlyList<Uuid>? evidenceRequestIds,
        ActorReference actor, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var evidence = Distinct(evidenceRequestIds);
        if (FindAction(riskId, actionId) is { } existing)
            return existing.Title == title?.Trim() && existing.DueOn == dueOn &&
                   existing.TargetState == targetState?.Trim() &&
                   existing.ExpectedEvidence == expectedEvidence?.Trim() &&
                   existing.AccountableMemberId == accountableMemberId &&
                   existing.EvidenceRequestIds.SequenceEqual(evidence)
                ? null
                : CommandFailure.StateConflict(
                    "The treatment action was already recorded with different content.");
        if (CheckRevision(riskId, expectedRevision) is { } stale)
            return stale;
        if (actionId == Uuid.Empty || accountableMemberId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(title) || title.Trim().Length > MaximumTitleLength ||
            InvalidText(targetState) || InvalidText(expectedEvidence))
            return CommandFailure.InvalidContent(
                "A treatment action requires a title of at most 200 characters, a target state, expected evidence, and an accountable member.");
        if (dueOn < DateOnly.FromDateTime(createdAt.UtcDateTime))
            return CommandFailure.InvalidContent("A treatment action cannot be due in the past.");
        if (riskTreatmentKind is null or "accept")
            return CommandFailure.StateConflict(
                "Choose a mitigate, transfer, or avoid treatment before adding treatment actions.");
        RaiseEvent(new RiskTreatmentActionAdded(_tenantId, Id, riskId, RevisionOf(riskId) + 1,
            new RiskTreatmentActionView(actionId, riskId, title.Trim(), targetState.Trim(),
                expectedEvidence.Trim(), dueOn, accountableMemberId, evidence, ActionOpen, false,
                actor, createdAt, [])));
        return null;
    }

    public CommandFailure? CancelTreatmentAction(Uuid riskId, Uuid actionId,
        long expectedRevision, Uuid cancellationId, string rationale, ActorReference actor,
        DateTimeOffset cancelledAt)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (FindAction(riskId, actionId) is not { } action)
            return CommandFailure.MissingRecord("The treatment action was not found.");
        if (action.Status == ActionCancelled)
            return _risks[riskId].CancellationIds.TryGetValue(actionId, out var existingId) &&
                   existingId == cancellationId &&
                   action.CancellationRationale == rationale?.Trim() &&
                   action.CancelledBy == actor
                ? null
                : CommandFailure.StateConflict("The treatment action is already cancelled.");
        if (CheckRevision(riskId, expectedRevision) is { } stale)
            return stale;
        if (action.Status is not (ActionOpen or ActionSubmitted))
            return CommandFailure.StateConflict(
                "Only unfinished treatment actions can be cancelled.");
        if (cancellationId == Uuid.Empty || InvalidText(rationale))
            return CommandFailure.InvalidContent(
                "Cancelling a treatment action requires a cancellation ID and a rationale of at most 4000 characters.");
        RaiseEvent(new RiskTreatmentActionCancelled(_tenantId, Id, riskId,
            RevisionOf(riskId) + 1, actionId, cancellationId, rationale.Trim(), actor,
            cancelledAt));
        return null;
    }

    /// <summary>Checks whether the request ID already records this exact edit.</summary>
    public CommandFailure? CheckTreatmentActionEditReplay(Uuid riskId, Uuid actionId,
        long expectedRevision, Uuid requestId, string title, string targetState,
        string expectedEvidence, DateOnly dueOn, Uuid accountableMemberId,
        IReadOnlyList<Uuid>? evidenceRequestIds, out bool handled)
    {
        handled = false;
        if (!_risks.TryGetValue(riskId, out var risk) ||
            !risk.ActionEditRequests.TryGetValue(requestId, out var recorded))
            return null;
        handled = true;
        var candidate = new ActionEditRequest(riskId, actionId, expectedRevision,
            title?.Trim() ?? string.Empty, targetState?.Trim() ?? string.Empty,
            expectedEvidence?.Trim() ?? string.Empty, dueOn, accountableMemberId,
            DistinctForReplay(evidenceRequestIds));
        return recorded.Matches(candidate)
            ? null
            : CommandFailure.StateConflict(
                "The treatment action edit request was already recorded with different content.");
    }

    /// <summary>Edits an open action and records request identity for safe retry.</summary>
    public CommandFailure? ReviseTreatmentAction(Uuid riskId, Uuid actionId,
        long expectedRevision, Uuid requestId, string title, string targetState,
        string expectedEvidence, DateOnly dueOn, Uuid accountableMemberId,
        IReadOnlyList<Uuid>? evidenceRequestIds, ActorReference actor,
        DateTimeOffset revisedAt)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var evidence = Distinct(evidenceRequestIds);
        if (CheckTreatmentActionEditReplay(riskId, actionId, expectedRevision, requestId,
                title, targetState, expectedEvidence, dueOn, accountableMemberId,
                evidenceRequestIds, out var replayed) is { } replayFailure)
            return replayFailure;
        if (replayed)
            return null;
        if (FindAction(riskId, actionId) is not { } action)
            return CommandFailure.MissingRecord("The treatment action was not found.");
        if (action.Status != ActionOpen)
            return CommandFailure.StateConflict(
                "Only open treatment actions can be edited.");
        if (CheckRevision(riskId, expectedRevision) is { } stale)
            return stale;
        if (requestId == Uuid.Empty || accountableMemberId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(title) || title.Trim().Length > MaximumTitleLength ||
            InvalidText(targetState) || InvalidText(expectedEvidence) ||
            evidenceRequestIds?.Any(static id => id == Uuid.Empty) is true)
            return CommandFailure.InvalidContent(
                "A treatment action edit requires a title of at most 200 characters, a target state, expected evidence, and an accountable member and valid evidence references.");
        if (dueOn < DateOnly.FromDateTime(revisedAt.UtcDateTime))
            return CommandFailure.InvalidContent("A treatment action cannot be due in the past.");

        RaiseEvent(new RiskTreatmentActionRevised(_tenantId, Id, riskId,
            RevisionOf(riskId) + 1, actionId, requestId, expectedRevision,
            title.Trim(), targetState.Trim(), expectedEvidence.Trim(), dueOn,
            accountableMemberId, evidence, actor, revisedAt));
        return null;
    }

    /// <param name="fulfilledEvidenceRequestIds">
    ///     The program evidence requests that are fulfilled now.
    /// </param>
    public CommandFailure? SubmitActionCompletion(Uuid riskId, Uuid actionId,
        long expectedRevision, Uuid submissionId, string summary,
        IReadOnlyList<Uuid> evidenceRequestIds, IReadOnlySet<Uuid> fulfilledEvidenceRequestIds,
        Uuid submitterMemberId, ActorReference submitter, DateTimeOffset submittedAt)
    {
        ArgumentNullException.ThrowIfNull(submitter);
        ArgumentNullException.ThrowIfNull(fulfilledEvidenceRequestIds);
        if (FindAction(riskId, actionId) is not { } action)
            return CommandFailure.MissingRecord("The treatment action was not found.");
        if (action.Completions.Any(item => item.SubmissionId == submissionId))
            return null;
        if (CheckRevision(riskId, expectedRevision) is { } stale)
            return stale;
        if (action.Status != ActionOpen)
            return CommandFailure.StateConflict(
                "The treatment action has no open work to complete.");
        var evidence = Distinct(evidenceRequestIds);
        if (submissionId == Uuid.Empty || InvalidText(summary) || evidence.Count == 0)
            return CommandFailure.InvalidContent(
                "A completion requires a summary of at most 4000 characters and at least one evidence request.");
        if (evidence.Any(id => !fulfilledEvidenceRequestIds.Contains(id)))
            return CommandFailure.StateConflict(
                "Every cited evidence request must be fulfilled before completion is submitted.");
        RaiseEvent(new RiskTreatmentActionCompletionSubmitted(_tenantId, Id, riskId,
            RevisionOf(riskId) + 1, actionId, submissionId, summary.Trim(), evidence,
            submitterMemberId, submitter, submittedAt));
        return null;
    }

    public CommandFailure? ReviewActionCompletion(Uuid riskId, Uuid actionId,
        long expectedRevision, Uuid decisionId, string outcome, string rationale,
        Uuid reviewerMemberId, ActorReference reviewer, DateTimeOffset reviewedAt,
        IReadOnlySet<Uuid> fulfilledEvidenceRequestIds, SeparationOfDutiesWaiver? waiver = null)
    {
        ArgumentNullException.ThrowIfNull(reviewer);
        ArgumentNullException.ThrowIfNull(fulfilledEvidenceRequestIds);
        if (FindAction(riskId, actionId) is not { } action)
            return CommandFailure.MissingRecord("The treatment action was not found.");
        if (action.Status != ActionSubmitted)
            return CommandFailure.StateConflict(
                "The treatment action has no pending completion to review.");
        if (CheckRevision(riskId, expectedRevision) is { } stale)
            return stale;
        var submission = action.Completions[^1];
        if (waiver is not null && (waiver.TenantId != _tenantId || !waiver.Allows(
                new SeparationOfDutiesWaiverScope(
                    SeparationOfDutiesRecordTypes.RiskTreatmentAction, actionId,
                    submission.SubmissionId, expectedRevision,
                    SeparationOfDutiesActions.Review), reviewerMemberId, reviewedAt)))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this member and completion.");
        if ((_risks[riskId].Submitters[submission.SubmissionId] == reviewerMemberId ||
             action.AccountableMemberId == reviewerMemberId) && waiver is null)
            return CommandFailure.ActorProhibited(
                "The member who submitted or is accountable for the action cannot review its completion.");
        if (decisionId == Uuid.Empty || outcome is not (Accept or Reject) ||
            InvalidText(rationale))
            return CommandFailure.InvalidContent(
                "A completion review requires an outcome of accept or reject and a rationale of at most 4000 characters.");
        if (outcome == Accept && submission.EvidenceRequestIds
                .Any(id => !fulfilledEvidenceRequestIds.Contains(id)))
            return CommandFailure.StateConflict(
                "Completion cannot be accepted while a cited evidence request is not fulfilled.");
        RaiseEvent(new RiskTreatmentActionCompletionReviewed(_tenantId, Id, riskId,
            RevisionOf(riskId) + 1, actionId, decisionId, outcome, rationale.Trim(), reviewer,
            reviewedAt, waiver?.Id));
        return null;
    }

    static List<Uuid> Distinct(IReadOnlyList<Uuid>? ids) =>
        [.. (ids ?? []).Where(static id => id != Uuid.Empty).Distinct()];

    static List<Uuid> DistinctForReplay(IReadOnlyList<Uuid>? ids) =>
        [.. (ids ?? []).Distinct()];

    /// <summary>Raises the trigger once; a replayed trigger is a no-op.</summary>
    public void RaiseTrigger(Uuid riskId, string triggerKind, string sourceReference,
        DateTimeOffset raisedAt)
    {
        if (triggerKind is not (MethodChanged or BoundaryChanged) ||
            string.IsNullOrWhiteSpace(sourceReference))
            throw new ArgumentException("A reassessment trigger requires a known kind and source.");
        var triggerId = TriggerIdFor(Id, riskId, triggerKind, sourceReference);
        if (_risks.TryGetValue(riskId, out var risk) &&
            risk.Triggers.Any(trigger => trigger.TriggerId == triggerId))
            return;
        RaiseEvent(new RiskReassessmentTriggered(_tenantId, Id, riskId, RevisionOf(riskId) + 1,
            new RiskReassessmentTriggerView(triggerId, riskId, triggerKind, sourceReference,
                raisedAt)));
    }

    CommandFailure? CheckRevision(Uuid riskId, long expectedRevision) =>
        expectedRevision != RevisionOf(riskId)
            ? CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("risk governance",
                RevisionOf(riskId)))
            : null;

    static bool InvalidText(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Length > MaximumTextLength;

    RiskState State(Uuid riskId)
    {
        if (!_risks.TryGetValue(riskId, out var risk))
        {
            risk = new RiskState();
            _risks.Add(riskId, risk);
        }
        return risk;
    }

    sealed class RiskState
    {
        public long Revision { get; set; }
        public RiskOwnerView? Owner { get; set; }
        public List<RiskControlTreatmentView> Treatments { get; } = [];
        public Dictionary<Uuid, Uuid> Proposers { get; } = [];
        public List<RiskReassessmentTriggerView> Triggers { get; } = [];
        public List<RiskTreatmentActionView> Actions { get; } = [];
        public Dictionary<Uuid, Uuid> CancellationIds { get; } = [];
        public Dictionary<Uuid, ActionEditRequest> ActionEditRequests { get; } = [];
        public Dictionary<Uuid, Uuid> Submitters { get; } = [];
    }

    sealed record ActionEditRequest(Uuid RiskId, Uuid ActionId, long ExpectedRevision,
        string Title, string TargetState, string ExpectedEvidence, DateOnly DueOn,
        Uuid AccountableMemberId, IReadOnlyList<Uuid> EvidenceRequestIds)
    {
        public bool Matches(ActionEditRequest other) => RiskId == other.RiskId &&
            ActionId == other.ActionId && ExpectedRevision == other.ExpectedRevision &&
            Title == other.Title && TargetState == other.TargetState &&
            ExpectedEvidence == other.ExpectedEvidence && DueOn == other.DueOn &&
            AccountableMemberId == other.AccountableMemberId &&
            EvidenceRequestIds.SequenceEqual(other.EvidenceRequestIds);
    }
}
