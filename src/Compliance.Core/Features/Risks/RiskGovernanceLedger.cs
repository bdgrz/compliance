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
    const int MaximumTextLength = 4000;

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

    public RiskGovernanceView View(Uuid riskId, IReadOnlyList<RiskAssessmentView> assessments)
    {
        ArgumentNullException.ThrowIfNull(assessments);
        _risks.TryGetValue(riskId, out var risk);
        return new RiskGovernanceView(_tenantId, Id, riskId, risk?.Revision ?? 0, risk?.Owner,
            risk?.Treatments.ToArray() ?? [],
            [.. (risk?.Triggers ?? []).Select(trigger => WithStatus(trigger, assessments))]);
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
    }
}
