using System.Globalization;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     One program's control operating plans and occurrences in one stream. Each control has a
///     plan line whose versions are proposed, independently approved, and superseded; each
///     occurrence keeps every attestation version and review decision. Concurrency is checked per
///     control plan line and per occurrence.
/// </summary>
public sealed class ControlOperationsLedger : Aggregate
{
    public const string PendingApproval = "pending_approval";
    public const string Approved = "approved";
    public const string Superseded = "superseded";
    public const string Expected = "expected";
    public const string Missed = "missed";
    public const string Open = "open";
    public const string Submitted = "submitted";
    public const string Returned = "returned";
    public const string ActionRequested = "action_requested";
    public const string Deferred = "deferred";
    public const string Complete = "complete";
    public const string Failed = "failed";
    public const string NotApplicable = "not_applicable";
    public const string Skipped = "skipped";
    const int MaximumTextLength = 4000;
    const int MaximumEvidence = 50;

    readonly Uuid _tenantId;
    readonly Dictionary<Uuid, PlanLine> _lines = [];
    readonly Dictionary<Uuid, OccurrenceState> _occurrences = [];

    public ControlOperationsLedger(Uuid tenantId, Uuid programId)
        : base(programId, new EventStreamAddress(tenantId.ToString(), "control-operations",
            programId.ToString()))
    {
        _tenantId = tenantId;
        On<ControlOperatingPlanProposed>(ev =>
        {
            var line = Line(ev.ControlId);
            line.Revision = ev.Revision;
            line.Plans.Add(ev.Plan);
        });
        On<ControlOperatingPlanApproved>(ev =>
        {
            var line = Line(ev.ControlId);
            line.Revision = ev.Revision;
            for (var index = 0; index < line.Plans.Count; index++)
            {
                var plan = line.Plans[index];
                if (plan.Status == Approved && plan.PlanVersionId != ev.PlanVersionId)
                    line.Plans[index] = plan with { Status = Superseded };
            }
            var pendingIndex = line.Plans.FindIndex(plan => plan.PlanVersionId == ev.PlanVersionId);
            var pending = line.Plans[pendingIndex];
            for (var index = 0; index < line.Plans.Count; index++)
            {
                var plan = line.Plans[index];
                if (plan.Status == Superseded && plan.EffectiveUntil is null)
                    line.Plans[index] = plan with { EffectiveUntil = pending.EffectiveFrom };
            }
            line.Plans[pendingIndex] = pending with
            {
                Status = Approved,
                Revision = ev.Revision,
                ApprovedBy = ev.ApprovedBy,
                ApprovedAt = ev.ApprovedAt,
                ApprovalRationale = ev.Rationale,
                ApprovalSeparationOfDutiesWaiverId = ev.SeparationOfDutiesWaiverId,
                Reassignments = ev.Reassignments,
            };
            foreach (var reassignment in ev.Reassignments)
                if (_occurrences.TryGetValue(reassignment.OccurrenceId, out var occurrence))
                {
                    occurrence.Assignee = reassignment.To;
                    occurrence.Reassignments.Add(reassignment);
                }
        });
        On<ControlOccurrenceOpened>(ev => _occurrences[ev.OccurrenceId] = new OccurrenceState(ev));
        On<ControlOccurrenceAttested>(ev =>
        {
            var occurrence = _occurrences[ev.OccurrenceId];
            occurrence.Revision = ev.Revision;
            occurrence.Attestations.Add(ev.Attestation);
            occurrence.State = Submitted;
        });
        On<ControlOccurrenceReviewed>(ev =>
        {
            var occurrence = _occurrences[ev.OccurrenceId];
            occurrence.Revision = ev.Revision;
            occurrence.Reviews.Add(ev.Review);
            occurrence.State = ev.Review.Outcome;
        });
    }

    public IEnumerable<Uuid> PlannedControlIds => _lines.Keys;

    /// <summary>The control of a recorded occurrence, or null when it was never recorded.</summary>
    public Uuid? OccurrenceControlId(Uuid occurrenceId) =>
        _occurrences.TryGetValue(occurrenceId, out var occurrence)
            ? occurrence.Opened.ControlId
            : null;

    public long PlanRevision(Uuid controlId) => _lines.GetValueOrDefault(controlId)?.Revision ?? 0;

    public ControlOperatingPlanView? CurrentPlan(Uuid controlId) =>
        _lines.GetValueOrDefault(controlId)?.Plans.LastOrDefault(static plan =>
            plan.Status == Approved);

    public ControlOperatingPlanView? PendingPlan(Uuid controlId) =>
        _lines.GetValueOrDefault(controlId)?.Plans.LastOrDefault(static plan =>
            plan.Status == PendingApproval);

    public ControlOperatingPlanView? FindPlan(Uuid controlId, Uuid planVersionId) =>
        _lines.GetValueOrDefault(controlId)?.Plans.Find(plan => plan.PlanVersionId == planVersionId);

    /// <summary>The approved or superseded plan whose effective interval contains the date.</summary>
    public ControlOperatingPlanView? PlanOn(Uuid controlId, DateOnly date) =>
        _lines.GetValueOrDefault(controlId)?.Plans.LastOrDefault(plan =>
            plan.Status is Approved or Superseded && plan.EffectiveFrom <= date &&
            (plan.EffectiveUntil is null || date < plan.EffectiveUntil));

    public ControlOperatingPlanSetView ReadPlans(Uuid controlId)
    {
        var line = _lines.GetValueOrDefault(controlId);
        return new ControlOperatingPlanSetView(controlId, line?.Revision ?? 0,
            CurrentPlan(controlId), PendingPlan(controlId), line?.Plans.ToArray() ?? []);
    }

    /// <summary>Materialized occurrences whose open work would move to a new owner.</summary>
    public IReadOnlyList<OccurrenceReassignmentView> OpenWorkToReassign(Uuid controlId,
        OperatingHolder newOwner, Uuid planVersionId) => _occurrences.Values
        .Where(occurrence => occurrence.Opened.ControlId == controlId &&
            occurrence.State is Open or Returned && occurrence.Assignee != newOwner)
        .OrderBy(static occurrence => occurrence.Opened.OpenedAt)
        .ThenBy(static occurrence => occurrence.Opened.OccurrenceId.ToString(),
            StringComparer.Ordinal)
        .Select(occurrence => new OccurrenceReassignmentView(occurrence.Opened.OccurrenceId,
            occurrence.Assignee, newOwner, planVersionId))
        .ToArray();

    public CommandFailure? ProposePlan(Uuid controlId, long expectedRevision, Uuid planVersionId,
        ControlVersionView version, OperatingHolder owner, OperatingHolder? backupOwner,
        Uuid reviewerMemberId, bool reviewerHoldsWork, ControlCadence cadence,
        DateOnly effectiveFrom, string rationale, Uuid proposerMemberId, string proposerDisplay,
        DateTimeOffset proposedAt, SeparationOfDutiesWaiver? waiver)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (FindPlan(controlId, planVersionId) is { } existing)
            return existing.ProposerMemberId == proposerMemberId
                ? null
                : CommandFailure.StateConflict("The operating plan request was already recorded.");
        if (expectedRevision != PlanRevision(controlId))
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "control operating plan", PlanRevision(controlId)));
        if (PendingPlan(controlId) is not null)
            return CommandFailure.StateConflict(
                "An operating plan is already pending approval for this control.");
        if (ValidatePlan(version, owner, backupOwner, reviewerMemberId, cadence, effectiveFrom,
                CurrentPlan(controlId)) is { } invalid)
            return invalid;
        if (!IsBoundedText(rationale))
            return CommandFailure.InvalidContent(
                "An operating plan requires a rationale of at most 4000 characters.");
        if (reviewerHoldsWork)
        {
            var scope = new SeparationOfDutiesWaiverScope(
                SeparationOfDutiesRecordTypes.ControlOperatingPlan, controlId, version.VersionId,
                expectedRevision + 1, SeparationOfDutiesActions.Review);
            if (waiver is null || waiver.TenantId != _tenantId ||
                !waiver.Allows(scope, reviewerMemberId, proposedAt))
                return CommandFailure.StateConflict(
                    "The reviewer also holds the control's operating work and requires an approved, active SoD waiver.");
        }
        else if (waiver is not null)
            return CommandFailure.InvalidContent(
                "A separation-of-duties waiver may only be used for a current conflict.");
        var clean = ControlCadenceSchedule.Clean(cadence);
        RaiseEvent(new ControlOperatingPlanProposed(_tenantId, Id, controlId, expectedRevision + 1,
            new ControlOperatingPlanView(_tenantId, Id, controlId, planVersionId,
                expectedRevision + 1, PendingApproval, version.VersionId, owner, backupOwner,
                reviewerMemberId, clean, ControlCadenceSchedule.Describe(clean),
                version.Content.ExpectedEvidenceDescriptions.ToArray(), effectiveFrom, null,
                rationale.Trim(), proposerMemberId,
                ActorReference.ForMember(proposerMemberId, proposerDisplay), proposedAt,
                waiver?.Id)));
        return null;
    }

    public static CommandFailure? ValidatePlan(ControlVersionView version, OperatingHolder? owner,
        OperatingHolder? backupOwner, Uuid reviewerMemberId, ControlCadence? cadence,
        DateOnly effectiveFrom, ControlOperatingPlanView? current)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (version.Status != Approved)
            return CommandFailure.StateConflict(
                "An operating plan must target the control's current approved version.");
        if (!IsHolder(owner) || backupOwner is not null && !IsHolder(backupOwner) ||
            backupOwner == owner || reviewerMemberId == Uuid.Empty)
            return CommandFailure.InvalidContent(
                "An operating plan requires an owner, an optional distinct backup owner, and a reviewer member. A holder kind is member, person, or team.");
        if (ControlCadenceSchedule.Validate(cadence) is { } cadenceError)
            return CommandFailure.InvalidContent(cadenceError);
        if (cadence!.FirstPeriodStart is { } first && first < effectiveFrom)
            return CommandFailure.InvalidContent(
                "The first cadence period cannot start before the plan's effective date.");
        if (effectiveFrom == default || effectiveFrom < version.EffectiveFrom ||
            version.EffectiveUntil is { } until && effectiveFrom >= until)
            return CommandFailure.InvalidContent(
                "The plan's effective date must fall inside the control version's effective interval.");
        return current is not null && effectiveFrom <= current.EffectiveFrom
            ? CommandFailure.InvalidContent(
                "A revised plan must take effect after the current plan's effective date.")
            : null;
    }

    public CommandFailure? ApprovePlan(Uuid controlId, long expectedRevision, Uuid planVersionId,
        string rationale, Uuid approverMemberId, string approverDisplay, DateTimeOffset approvedAt,
        SeparationOfDutiesWaiver? waiver)
    {
        if (FindPlan(controlId, planVersionId) is not { } plan)
            return CommandFailure.MissingRecord("The operating plan was not found.");
        if (plan.Status != PendingApproval)
            return CommandFailure.StateConflict("The operating plan is not pending approval.");
        if (expectedRevision != PlanRevision(controlId))
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "control operating plan", PlanRevision(controlId)));
        var scope = new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.ControlOperatingPlan, controlId, planVersionId,
            plan.Revision, SeparationOfDutiesActions.Approve);
        if (waiver is not null && (waiver.TenantId != _tenantId ||
                                   !waiver.Allows(scope, approverMemberId, approvedAt)))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this member and plan revision.");
        if (plan.ProposerMemberId == approverMemberId && waiver is null)
            return CommandFailure.ActorProhibited(
                "The member who proposed an operating plan cannot approve it.");
        if (!IsBoundedText(rationale))
            return CommandFailure.InvalidContent(
                "An operating plan approval requires a rationale of at most 4000 characters.");
        RaiseEvent(new ControlOperatingPlanApproved(_tenantId, Id, controlId, expectedRevision + 1,
            planVersionId, approverMemberId,
            ActorReference.ForMember(approverMemberId, approverDisplay), approvedAt,
            rationale.Trim(), waiver?.Id, OpenWorkToReassign(controlId, plan.Owner, planVersionId)));
        return null;
    }

    public CommandFailure? OpenOccurrence(Uuid controlId, Uuid occurrenceId, string trigger,
        DateOnly occurredOn, IReadOnlyDictionary<Uuid, DateOnly?> versionUntil,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset openedAt)
    {
        ArgumentNullException.ThrowIfNull(versionUntil);
        if (_occurrences.ContainsKey(occurrenceId))
            return null;
        if (PlanOn(controlId, occurredOn) is not { } plan)
            return CommandFailure.StateConflict(
                "The control has no approved operating plan effective on that date.");
        if (plan.Cadence.Kind == ControlCadenceSchedule.Recurring)
            return CommandFailure.StateConflict(
                "A recurring control operates through its expected occurrences.");
        if (!IsActive(versionUntil, plan.ControlVersionId, occurredOn))
            return CommandFailure.StateConflict("The control version is not effective on that date.");
        if (string.IsNullOrWhiteSpace(trigger) || trigger.Trim().Length > MaximumTextLength ||
            occurredOn > DateOnly.FromDateTime(openedAt.UtcDateTime))
            return CommandFailure.InvalidContent(
                "An occurrence requires a trigger of at most 4000 characters and a date that is not in the future.");
        RaiseEvent(new ControlOccurrenceOpened(_tenantId, Id, controlId, occurrenceId, 1,
            plan.Cadence.Kind, plan.ControlVersionId, plan.PlanVersionId, occurredOn, occurredOn,
            occurredOn.AddDays(plan.Cadence.DueWithinDays ?? 0), trigger.Trim(), plan.Owner,
            ActorReference.ForMember(actorMemberId, actorDisplay), openedAt));
        return null;
    }

    /// <summary>Resolves an occurrence, materialized or still expected, as of the date.</summary>
    public ControlOccurrenceView? ReadOccurrence(Uuid controlId, Uuid occurrenceId,
        IReadOnlyDictionary<Uuid, DateOnly?> versionUntil, DateOnly today)
    {
        if (_occurrences.TryGetValue(occurrenceId, out var occurrence))
            return occurrence.Opened.ControlId == controlId ? ToView(occurrence, today) : null;
        return ExpectedOccurrences(controlId, versionUntil, today,
                today.AddDays(ControlCadenceSchedule.MaximumDueWithinDays))
            .FirstOrDefault(view => view.OccurrenceId == occurrenceId);
    }

    /// <summary>Recorded occurrences and unrecorded expected periods starting on or before the horizon.</summary>
    public IReadOnlyList<ControlOccurrenceView> ReadOccurrences(Uuid controlId,
        IReadOnlyDictionary<Uuid, DateOnly?> versionUntil, DateOnly today, DateOnly horizon) =>
        _occurrences.Values.Where(occurrence => occurrence.Opened.ControlId == controlId)
            .Select(occurrence => ToView(occurrence, today))
            .Concat(ExpectedOccurrences(controlId, versionUntil, today, horizon))
            .OrderBy(static view => view.DueOn ?? DateOnly.MaxValue)
            .ThenBy(static view => view.OccurrenceId.ToString(), StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<ControlOccurrenceView> ExpectedOccurrences(Uuid controlId,
        IReadOnlyDictionary<Uuid, DateOnly?> versionUntil, DateOnly today, DateOnly horizon)
    {
        ArgumentNullException.ThrowIfNull(versionUntil);
        if (!_lines.TryGetValue(controlId, out var line))
            return [];
        var views = new List<ControlOccurrenceView>();
        foreach (var plan in line.Plans.Where(static plan => plan.Status is Approved or Superseded))
        {
            var until = Earliest(plan.EffectiveUntil,
                versionUntil.TryGetValue(plan.ControlVersionId, out var end) ? end : null);
            foreach (var period in ControlCadenceSchedule.Periods(plan.Cadence,
                         plan.EffectiveFrom, until, horizon))
            {
                var occurrenceId = ControlCadenceSchedule.OccurrenceId(controlId, period.Start);
                if (_occurrences.ContainsKey(occurrenceId))
                    continue;
                views.Add(new ControlOccurrenceView(_tenantId, Id, controlId, occurrenceId, 0,
                    Expected, period.DueOn < today ? Missed : Expected, plan.ControlVersionId,
                    plan.PlanVersionId, period.Start, period.End, period.DueOn, null,
                    CurrentPlan(controlId)?.Owner ?? plan.Owner, [], [], []));
            }
        }
        return views;
    }

    public CommandFailure? Attest(Uuid controlId, Uuid occurrenceId, long expectedRevision,
        AttestationInput input, IReadOnlyDictionary<Uuid, DateOnly?> versionUntil,
        Uuid recorderMemberId, string recorderDisplay, DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(input);
        var today = DateOnly.FromDateTime(recordedAt.UtcDateTime);
        if (ReadOccurrence(controlId, occurrenceId, versionUntil, today) is not { } current)
            return CommandFailure.MissingRecord("The control occurrence was not found.");
        if (current.Attestations.Count > 0)
            return CommandFailure.StateConflict(
                "The occurrence already has an attestation. Correct it by a new version instead.");
        if (expectedRevision != current.Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "control occurrence", current.Revision));
        if (current.PeriodStart is { } start && start > today)
            return CommandFailure.StateConflict("An expected occurrence cannot be attested before its period starts.");
        var plan = FindPlan(controlId, current.PlanVersionId)!;
        if (ValidateAttestation(input, plan.ExpectedEvidence, recordedAt) is { } invalid)
            return invalid;
        if (current.Revision == 0)
            RaiseEvent(new ControlOccurrenceOpened(_tenantId, Id, controlId, occurrenceId, 1,
                Expected, current.ControlVersionId, current.PlanVersionId, current.PeriodStart,
                current.PeriodEnd, current.DueOn, null, current.Assignee,
                ActorReference.ForMember(recorderMemberId, recorderDisplay), recordedAt));
        RaiseAttestation(controlId, occurrenceId, input, plan, current, null, null,
            recorderMemberId, recorderDisplay, recordedAt);
        return null;
    }

    public CommandFailure? Correct(Uuid controlId, Uuid occurrenceId, long expectedRevision,
        AttestationInput input, string correctionReason, Uuid recorderMemberId,
        string recorderDisplay, DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!_occurrences.TryGetValue(occurrenceId, out var occurrence) ||
            occurrence.Opened.ControlId != controlId)
            return CommandFailure.MissingRecord("The control occurrence was not found.");
        if (occurrence.Attestations.Count == 0)
            return CommandFailure.StateConflict("The occurrence has no attestation to correct.");
        if (expectedRevision != occurrence.Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "control occurrence", occurrence.Revision));
        if (!IsBoundedText(correctionReason))
            return CommandFailure.InvalidContent(
                "A correction requires a reason of at most 4000 characters.");
        var plan = FindPlan(controlId, occurrence.Opened.PlanVersionId)!;
        if (ValidateAttestation(input, plan.ExpectedEvidence, recordedAt) is { } invalid)
            return invalid;
        var view = ToView(occurrence, DateOnly.FromDateTime(recordedAt.UtcDateTime));
        RaiseAttestation(controlId, occurrenceId, input, plan, view, correctionReason.Trim(),
            occurrence.Attestations[^1].AttestationId, recorderMemberId, recorderDisplay,
            recordedAt);
        return null;
    }

    void RaiseAttestation(Uuid controlId, Uuid occurrenceId, AttestationInput input,
        ControlOperatingPlanView plan, ControlOccurrenceView current, string? correctionReason,
        Uuid? supersedes, Uuid recorderMemberId, string recorderDisplay, DateTimeOffset recordedAt)
    {
        var version = current.Attestations.Count + 1;
        var attestation = new ControlAttestationView(
            Uuid.CreateVersion5(occurrenceId, "attestation-" + version.ToString(
                CultureInfo.InvariantCulture)),
            version, input.Result, input.PerformedAt, input.CoveredFrom ?? current.PeriodStart,
            input.CoveredUntil ?? current.PeriodEnd, Trim(input.Notes), Trim(input.Rationale),
            input.Evidence.Select(static evidence => new EvidenceReference(
                evidence.ExpectedEvidenceIndex, evidence.Kind, evidence.Reference.Trim(),
                Trim(evidence.Description), "unresolved")).ToArray(),
            input.PerformedBy, recorderMemberId,
            ActorReference.ForMember(recorderMemberId, recorderDisplay), recordedAt,
            plan.ControlVersionId, plan.PlanVersionId, plan.ExpectedEvidence, correctionReason,
            supersedes);
        var revision = _occurrences.TryGetValue(occurrenceId, out var occurrence)
            ? occurrence.Revision + 1
            : 2;
        RaiseEvent(new ControlOccurrenceAttested(_tenantId, Id, controlId, occurrenceId, revision,
            attestation));
    }

    static CommandFailure? ValidateAttestation(AttestationInput input,
        IReadOnlyList<string> expectedEvidence, DateTimeOffset recordedAt)
    {
        if (input.Result is not (Complete or Failed or NotApplicable or Skipped))
            return CommandFailure.InvalidContent(
                "An attestation result must be complete, failed, not_applicable, or skipped.");
        if (input.PerformedBy.Kind is not ("member" or "person"))
            return CommandFailure.InvalidContent(
                "The performer of record must be a member or a workforce person.");
        if (input.PerformedAt == default || input.PerformedAt > recordedAt)
            return CommandFailure.InvalidContent("The performed time cannot be in the future.");
        if (input.CoveredFrom is { } from && input.CoveredUntil is { } until && until < from)
            return CommandFailure.InvalidContent("The covered period must end on or after it starts.");
        if (input.Notes is { } notes && notes.Trim().Length > MaximumTextLength)
            return CommandFailure.InvalidContent("Attestation notes must be at most 4000 characters.");
        if (input.Result != Complete && !IsBoundedText(input.Rationale))
            return CommandFailure.InvalidContent(
                "A failed, not_applicable, or skipped result requires a rationale of at most 4000 characters.");
        if (input.Evidence.Count > MaximumEvidence || input.Evidence.Any(evidence =>
                evidence is null || evidence.Kind is not ("artifact" or "record" or "external") ||
                string.IsNullOrWhiteSpace(evidence.Reference) || evidence.Reference.Length > 2000 ||
                evidence.Description is { Length: > 2000 } ||
                evidence.ExpectedEvidenceIndex is { } index &&
                (index < 0 || index >= expectedEvidence.Count)))
            return CommandFailure.InvalidContent(
                "Each evidence reference needs a kind of artifact, record, or external, a reference of at most 2000 characters, and a valid expected evidence index.");
        if (input.Result != Complete)
            return null;
        var covered = input.Evidence.Where(static evidence => evidence.ExpectedEvidenceIndex is not null)
            .Select(static evidence => evidence.ExpectedEvidenceIndex!.Value).ToHashSet();
        return input.Evidence.Count == 0 ||
               Enumerable.Range(0, expectedEvidence.Count).Any(index => !covered.Contains(index))
            ? CommandFailure.InvalidContent(
                "A complete result requires support for every expected evidence item.")
            : null;
    }

    public CommandFailure? Review(Uuid controlId, Uuid occurrenceId, long expectedRevision,
        Uuid attestationId, Uuid decisionId, string outcome, string rationale,
        IReadOnlyList<string> requestedActions, Uuid reviewerMemberId, string reviewerDisplay,
        DateTimeOffset reviewedAt, SeparationOfDutiesWaiver? waiver)
    {
        ArgumentNullException.ThrowIfNull(requestedActions);
        if (!_occurrences.TryGetValue(occurrenceId, out var occurrence) ||
            occurrence.Opened.ControlId != controlId)
            return CommandFailure.MissingRecord("The control occurrence was not found.");
        if (occurrence.Reviews.Find(review => review.DecisionId == decisionId) is not null)
            return null;
        if (expectedRevision != occurrence.Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "control occurrence", occurrence.Revision));
        if (occurrence.State is not (Submitted or Deferred))
            return CommandFailure.StateConflict("The occurrence has no attestation awaiting review.");
        var attestation = occurrence.Attestations[^1];
        if (attestation.AttestationId != attestationId)
            return CommandFailure.StateConflict("Review the latest attestation version.");
        var scope = new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.ControlOccurrence, occurrenceId, attestationId,
            attestation.Version, SeparationOfDutiesActions.Review);
        if (waiver is not null && (waiver.TenantId != _tenantId ||
                                   !waiver.Allows(scope, reviewerMemberId, reviewedAt)))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this member and attestation.");
        var performedOrRecorded = attestation.RecorderMemberId == reviewerMemberId ||
                                  attestation.PerformedBy is { Kind: "member" } performer &&
                                  performer.Id == reviewerMemberId;
        if (performedOrRecorded && waiver is null)
            return CommandFailure.ActorProhibited(
                "The member who performed or recorded an attestation cannot review it.");
        if (outcome is not (Approved or Returned or ActionRequested or Deferred) ||
            !IsBoundedText(rationale))
            return CommandFailure.InvalidContent(
                "A review requires an outcome of approved, returned, action_requested, or deferred and a rationale of at most 4000 characters.");
        if (outcome == ActionRequested
                ? requestedActions.Count is < 1 or > 20 ||
                  requestedActions.Any(static action => !IsBoundedText(action))
                : requestedActions.Count > 0)
            return CommandFailure.InvalidContent(
                "Requested actions are required for action_requested, at most 20 of at most 4000 characters, and not allowed otherwise.");
        RaiseEvent(new ControlOccurrenceReviewed(_tenantId, Id, controlId, occurrenceId,
            occurrence.Revision + 1, new ControlOccurrenceReviewView(decisionId, attestationId,
                attestation.Version, outcome, rationale.Trim(),
                requestedActions.Select(static action => action.Trim()).ToArray(),
                reviewerMemberId, ActorReference.ForMember(reviewerMemberId, reviewerDisplay),
                reviewedAt, waiver?.Id)));
        return null;
    }

    ControlOccurrenceView ToView(OccurrenceState occurrence, DateOnly today)
    {
        var opened = occurrence.Opened;
        var state = occurrence.State == Open && opened.DueOn is { } due && due < today
            ? Missed
            : occurrence.State;
        return new ControlOccurrenceView(_tenantId, Id, opened.ControlId, opened.OccurrenceId,
            occurrence.Revision, opened.Kind, state, opened.ControlVersionId,
            opened.PlanVersionId, opened.PeriodStart, opened.PeriodEnd, opened.DueOn,
            opened.Trigger, occurrence.Assignee, occurrence.Attestations.ToArray(),
            occurrence.Reviews.ToArray(), occurrence.Reassignments.ToArray());
    }

    public static bool IsActive(IReadOnlyDictionary<Uuid, DateOnly?> versionUntil,
        Uuid versionId, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(versionUntil);
        return !versionUntil.TryGetValue(versionId, out var until) || until is null ||
               date < until;
    }

    static DateOnly? Earliest(DateOnly? first, DateOnly? second) =>
        first is null ? second : second is null ? first : first < second ? first : second;

    static bool IsHolder(OperatingHolder? holder) => holder is not null &&
        holder.Id != Uuid.Empty && holder.Kind is "member" or "person" or "team";

    static bool IsBoundedText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= MaximumTextLength;

    static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    PlanLine Line(Uuid controlId)
    {
        if (!_lines.TryGetValue(controlId, out var line))
        {
            line = new PlanLine();
            _lines.Add(controlId, line);
        }
        return line;
    }

    sealed class PlanLine
    {
        public long Revision { get; set; }
        public List<ControlOperatingPlanView> Plans { get; } = [];
    }

    sealed class OccurrenceState(ControlOccurrenceOpened opened)
    {
        public ControlOccurrenceOpened Opened { get; } = opened;
        public long Revision { get; set; } = opened.Revision;
        public string State { get; set; } = Open;
        public OperatingHolder Assignee { get; set; } = opened.Assignee;
        public List<ControlAttestationView> Attestations { get; } = [];
        public List<ControlOccurrenceReviewView> Reviews { get; } = [];
        public List<OccurrenceReassignmentView> Reassignments { get; } = [];
    }
}
