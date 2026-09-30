using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class ControlDraft : Aggregate
{
    // Fitz stream events use a u16 frame. Reserve room for the Portia envelope,
    // stream address, metadata, and framing after the serialized domain event.
    public const int MaximumDraftEventPayloadBytes = 48 * 1024;
    readonly Uuid _tenantId;
    bool _created;
    long _revision;
    string? _identifier;
    Uuid _createRequestId;
    ControlDraftContent? _initialContent;
    ControlDraftContent? _currentContent;
    bool _discarded;
    Uuid _draftAuthorMemberId;
    bool _everReviewed;
    Uuid _acceptedReviewDecisionId;
    Uuid? _latestReviewDecisionId;
    ControlVersionView? _approvedVersion;
    readonly List<ControlDecisionView> _decisions = [];
    readonly Dictionary<ResponsibilityScope, ResponsibilitySet> _responsibilitySets = [];
    readonly Dictionary<ResponsibilityScope, List<ResponsibilityDecisionFact>> _responsibilityDecisions = [];

    public bool IsCreated => _created;
    public Uuid DraftVersionId => ControlVersionIds.Initial(Id);
    public bool IsApproved => _approvedVersion is not null;
    public ControlDraftContent? CurrentContent => _currentContent;
    public ControlVersionView? ApprovedVersion => _approvedVersion;
    public IReadOnlyList<ControlDecisionView> ReadDecisions() => _decisions.ToArray();
    public bool IsVisible => _created && !_discarded;
    public long Revision => _revision;
    public Uuid ProgramId { get; private set; }

    public ControlDraft(Uuid tenantId, Uuid controlId)
        : base(controlId, new EventStreamAddress(tenantId.ToString(), "controls", controlId.ToString()))
    {
        _tenantId = tenantId;
        On<ControlDraftCreated>(ev =>
        {
            _created = true;
            _revision = 1;
            ProgramId = ev.ProgramId;
            _identifier = ev.Identifier;
            _createRequestId = ev.CreateRequestId;
            _initialContent = ev.Content;
            _currentContent = ev.Content;
            _draftAuthorMemberId = ev.ActorMemberId;
        });
        On<ControlDraftRevised>(ev =>
        {
            _revision = ev.Revision;
            _currentContent = ev.Content;
            _draftAuthorMemberId = ev.ActorMemberId;
            _acceptedReviewDecisionId = Uuid.Empty;
            _latestReviewDecisionId = null;
        });
        On<ControlReviewed>(ev =>
        {
            _everReviewed = true;
            _acceptedReviewDecisionId = ev.Outcome == "accept" ? ev.DecisionId : Uuid.Empty;
            _latestReviewDecisionId = ev.DecisionId;
            _decisions.Add(new ControlDecisionView(ev.TenantId, ev.ProgramId, ev.ControlId,
                ev.DecisionId, ev.VersionId, ev.Revision, "review", ev.Outcome, ev.Actor,
                ev.Rationale, ev.DecidedAt, ev.SupersedesDecisionId, null,
                ev.SeparationOfDutiesWaiverId));
            RecordResponsibilityDecision(Scope(ev.VersionId, ev.Revision), ev.ActorMemberId,
                ResponsibilityType.AssignedReviewer, ev.DecidedAt, ev.SeparationOfDutiesWaiverId);
        });
        On<ControlApproved>(ev =>
        {
            _decisions.Add(new ControlDecisionView(ev.TenantId, ev.ProgramId, ev.ControlId,
                ev.ApprovalDecisionId, ev.VersionId, ev.Revision, "approval", "approve",
                ev.Actor, ev.Rationale, ev.DecidedAt, null, ev.AcceptedReviewDecisionId,
                ev.SeparationOfDutiesWaiverId));
            RecordResponsibilityDecision(Scope(ev.VersionId, ev.Revision), ev.ActorMemberId,
                ResponsibilityType.PolicyApprover, ev.DecidedAt, ev.SeparationOfDutiesWaiverId);
            _approvedVersion = new ControlVersionView(ev.TenantId, ev.ProgramId, ev.ControlId,
                _identifier!, ev.VersionId, ev.Revision, "approved", "organization_authored",
                ev.Content, ev.EffectiveFrom, null, ev.OwnerAssignmentId, ev.OwnerMemberId,
                "verified_member", ev.AcceptedReviewDecisionId, ev.ApprovalDecisionId,
                ev.Actor, ev.Rationale, ev.DecidedAt, ev.SeparationOfDutiesWaiverId);
            _acceptedReviewDecisionId = Uuid.Empty;
        });
        On<ResponsibilityAssigned>(ev => GetResponsibilitySet(ev.Scope).Apply(ev));
        On<ResponsibilityRevoked>(ev => GetResponsibilitySet(ev.Scope).Apply(ev));
        On<ControlDraftDiscarded>(ev =>
        {
            _revision = ev.Revision;
            _discarded = true;
        });
    }

    public static string NormalizeIdentifier(string? identifier) =>
        identifier?.Trim().ToUpperInvariant() ?? string.Empty;

    public static Uuid IdFor(Uuid tenantId, Uuid programId, string identifier) =>
        Uuid.CreateVersion5(Uuid.CreateVersion5(tenantId, programId.ToString()),
            NormalizeIdentifier(identifier));

    internal static RequestError? ValidateCreateInput(string? identifier,
        ControlDraftContent? content) => Validate(NormalizeIdentifier(identifier), content);

    public Result<ControlRegistration> Create(Uuid programId, Uuid createRequestId, string identifier,
        ControlDraftContent content, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt)
    {
        var normalized = NormalizeIdentifier(identifier);
        var error = Validate(normalized, content);
        if (error is not null)
            return Result<ControlRegistration>.Failure(error);
        var clean = Clean(content);
        if (_created)
        {
            if (_discarded)
                return Result<ControlRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The discarded control draft cannot be recreated."));
            return ProgramId == programId && _createRequestId == createRequestId &&
                   StringComparer.Ordinal.Equals(_identifier, normalized) &&
                   Equal(_initialContent!, clean)
                ? Result<ControlRegistration>.Success(new ControlRegistration(Id, normalized, 1))
                : Result<ControlRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The control identifier already exists with different draft content."));
        }
        ControlDraftCreated created = new(_tenantId, programId, Id, createRequestId,
            normalized, clean, actorMemberId, actorDisplay, changedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        };
        if (JsonSerializer.SerializeToUtf8Bytes(created,
                ComplianceCoreJsonContext.Default.ControlDraftCreated).Length >
            MaximumDraftEventPayloadBytes)
            return Result<ControlRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "The control draft exceeds the bounded event payload size."));
        RaiseEvent(created);
        return Result<ControlRegistration>.Success(new ControlRegistration(Id, normalized, 1));
    }

    public CommandFailure? Revise(Uuid programId, long expectedRevision, ControlDraftContent content,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt)
    {
        if (!_created || _discarded || ProgramId != programId)
            return CommandFailure.MissingRecord("The control draft was not found.");
        if (IsApproved)
            return CommandFailure.StateConflict(
                "The approved control version is immutable. Successor drafts are not yet supported.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("control draft",
                _revision));
        var error = Validate(_identifier!, content);
        if (error is not null)
            return CommandFailure.InvalidContent(error.Message!);
        ControlDraftRevised revised = new(_tenantId, programId, Id, _revision + 1,
            Clean(content), actorMemberId, actorDisplay, changedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        };
        if (JsonSerializer.SerializeToUtf8Bytes(revised,
                ComplianceCoreJsonContext.Default.ControlDraftRevised).Length >
            MaximumDraftEventPayloadBytes)
            return CommandFailure.InvalidContent("The control draft exceeds the bounded event payload size.");
        RaiseEvent(revised);
        return null;
    }

    public CommandFailure? Discard(Uuid programId, long expectedRevision, string rationale,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset discardedAt)
    {
        if (!_created || _discarded || ProgramId != programId)
            return CommandFailure.MissingRecord("The control draft was not found.");
        if (IsApproved)
            return CommandFailure.StateConflict("An approved control cannot be discarded.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("control draft",
                _revision));
        if (VersionedRecordRules.DraftDiscardConflict(_everReviewed) is { } referenced)
            return CommandFailure.ForVersion(referenced);
        if (_currentContent?.Applicability is { Count: > 0 })
            return CommandFailure.StateConflict(
                "Discarding a control draft requires no retained applicability relationships.");
        if (string.IsNullOrWhiteSpace(rationale))
            return CommandFailure.InvalidContent("Discarding a control draft requires a rationale.");
        var discarded = new ControlDraftDiscarded(_tenantId, programId, Id, _revision,
            actorMemberId, actorDisplay, rationale.Trim(), discardedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        };
        if (JsonSerializer.SerializeToUtf8Bytes(discarded,
                ComplianceCoreJsonContext.Default.ControlDraftDiscarded).Length >
            MaximumDraftEventPayloadBytes)
            return CommandFailure.InvalidContent(
                "The control draft discard exceeds the bounded event payload size.");
        RaiseEvent(discarded);
        return null;
    }

    ResponsibilityScope Scope(Uuid versionId, long revision) =>
        new(SeparationOfDutiesRecordTypes.Control, Id, versionId, revision);

    public ResponsibilitySet GetResponsibilitySet(ResponsibilityScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (!StringComparer.Ordinal.Equals(scope.RecordType, SeparationOfDutiesRecordTypes.Control) ||
            scope.RecordId != Id)
            throw new ArgumentException("The responsibility scope must belong to this control.",
                nameof(scope));
        if (!_responsibilitySets.TryGetValue(scope, out var set))
        {
            set = new ResponsibilitySet(_tenantId, scope);
            _responsibilitySets.Add(scope, set);
        }
        return set;
    }

    /// <summary>Whether the scope is the exact current, unapproved draft revision.</summary>
    public bool IsCurrentResponsibilityScope(ResponsibilityScope scope) =>
        scope is not null &&
        StringComparer.Ordinal.Equals(scope.RecordType, SeparationOfDutiesRecordTypes.Control) &&
        scope.RecordId == Id && IsVisible && !IsApproved && scope.VersionId == DraftVersionId &&
        scope.Revision == _revision;

    /// <summary>Members holding a control_owner responsibility on the current exact revision.</summary>
    public IReadOnlyList<Uuid> CurrentOwnerMemberIds(DateTimeOffset at) =>
        !IsVisible || IsApproved
            ? []
            : ActiveOwners(Scope(DraftVersionId, _revision), at)
                .Select(static assignment => assignment.MemberId).Distinct().ToArray();

    IEnumerable<ResponsibilityAssignmentView> ActiveOwners(ResponsibilityScope scope,
        DateTimeOffset at) => GetResponsibilitySet(scope).ReadAssignments()
        .Where(assignment => assignment.Type == ResponsibilityType.ControlOwner &&
            assignment.RevokedAt is null && assignment.EffectiveFrom <= at &&
            (assignment.EffectiveUntil is null || assignment.EffectiveUntil > at));

    public CommandFailure? AssignResponsibility(ResponsibilityScope scope, Uuid assignmentId,
        Uuid memberId, ResponsibilityType type, Uuid assignedByMemberId,
        string assignedByDisplay, DateTimeOffset assignedAt, DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveUntil, IReadOnlyList<SeparationOfDutiesWaiver> waivers)
    {
        if (!IsCurrentResponsibilityScope(scope))
            return CommandFailure.StateConflict(
                "Responsibilities must target the current exact control draft revision.");
        var set = GetResponsibilitySet(scope);
        var proposed = new ResponsibilityAssignmentView(_tenantId, assignmentId, memberId,
            type, scope, assignedAt, assignedByMemberId, effectiveFrom, effectiveUntil,
            null, Uuid.Empty, []);
        if (HasUnwaivedHistoricalConflict(scope, set.ReadAssignments().Append(proposed)))
            return CommandFailure.StateConflict(
                "The responsibility would create an unwaived conflict with a prior decision on this exact revision.");
        return set.Assign(assignmentId, memberId, type, assignedByMemberId, assignedByDisplay,
            assignedAt, effectiveFrom, effectiveUntil, waivers, RaiseEvent);
    }

    public CommandFailure? RevokeResponsibility(ResponsibilityScope scope, Uuid assignmentId,
        Uuid memberId, string memberDisplay, DateTimeOffset revokedAt, string reason)
    {
        if (!IsCurrentResponsibilityScope(scope))
            return CommandFailure.StateConflict(
                "Responsibilities must target the current exact control draft revision.");
        return GetResponsibilitySet(scope).Revoke(assignmentId, memberId, memberDisplay,
            revokedAt, reason, RaiseEvent);
    }

    void RecordResponsibilityDecision(ResponsibilityScope scope, Uuid memberId,
        ResponsibilityType type, DateTimeOffset at, Uuid? waiverId)
    {
        if (!_responsibilityDecisions.TryGetValue(scope, out var decisions))
        {
            decisions = [];
            _responsibilityDecisions.Add(scope, decisions);
        }
        decisions.Add(new ResponsibilityDecisionFact(memberId, type, at, waiverId));
    }

    bool HasUnwaivedHistoricalConflict(ResponsibilityScope scope,
        IEnumerable<ResponsibilityAssignmentView> assignments)
    {
        if (!_responsibilityDecisions.TryGetValue(scope, out var decisions))
            return false;
        var candidates = assignments.ToArray();
        foreach (var decision in decisions.Where(static decision => decision.WaiverId is null))
        {
            var decidedAction = new ResponsibilityAssignmentView(_tenantId, Uuid.Empty,
                decision.MemberId, decision.Type, scope, decision.At, Uuid.Empty,
                decision.At, decision.At.AddTicks(1), null, Uuid.Empty, []);
            if (ResponsibilityConflictPolicy.FindConflicts(candidates, decidedAction).Count > 0)
                return true;
        }
        return false;
    }

    sealed record ResponsibilityDecisionFact(Uuid MemberId, ResponsibilityType Type,
        DateTimeOffset At, Uuid? WaiverId);

    CommandFailure? CheckDecisionTarget(Uuid programId, long expectedRevision)
    {
        if (!IsVisible || ProgramId != programId)
            return CommandFailure.MissingRecord("The control draft was not found.");
        if (IsApproved)
            return CommandFailure.StateConflict("The control version is already approved.");
        return expectedRevision != _revision
            ? CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("control draft",
                _revision))
            : null;
    }

    CommandFailure? CheckSeparationOfDuties(ResponsibilityScope scope, Uuid memberId,
        ResponsibilityType decisionType, string action, DateTimeOffset at,
        SeparationOfDutiesWaiver? waiver)
    {
        var isAuthor = memberId == _draftAuthorMemberId;
        var failure = ResponsibilityDecisionGuard.Validate(GetResponsibilitySet(scope), scope,
            memberId, decisionType, at, isAuthor, waiver);
        if (failure is not null)
            return failure;
        var waiverScope = new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.Control,
            Id, scope.VersionId, scope.Revision, action);
        if (waiver is not null &&
            (waiver.TenantId != _tenantId || !waiver.Allows(waiverScope, memberId, at)))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this member and draft revision.");
        return isAuthor && waiver is null
            ? CommandFailure.ActorProhibited(
                "A control draft author cannot " + action + " their own draft revision.")
            : null;
    }

    public CommandFailure? Review(Uuid programId, long expectedRevision, Uuid decisionId,
        string outcome, string rationale, Uuid reviewerMemberId, string reviewerDisplay,
        DateTimeOffset decidedAt, SeparationOfDutiesWaiver? separationOfDutiesWaiver = null)
    {
        if (CheckDecisionTarget(programId, expectedRevision) is { } target)
            return target;
        var scope = Scope(DraftVersionId, expectedRevision);
        if (CheckSeparationOfDuties(scope, reviewerMemberId, ResponsibilityType.AssignedReviewer,
                SeparationOfDutiesActions.Review, decidedAt, separationOfDutiesWaiver) is { } sod)
            return sod;
        if (decisionId == Uuid.Empty || outcome is not ("accept" or "request_changes") ||
            string.IsNullOrWhiteSpace(rationale) || rationale.Length > 4000)
            return CommandFailure.InvalidContent(
                "A control review requires an outcome of accept or request_changes and a rationale of at most 4000 characters.");
        RaiseEvent(new ControlReviewed(_tenantId, ProgramId, Id, DraftVersionId,
            expectedRevision, decisionId, outcome, reviewerMemberId, reviewerDisplay,
            rationale.Trim(), decidedAt, _latestReviewDecisionId, separationOfDutiesWaiver?.Id)
        {
            StoredActor = ActorReference.ForMember(reviewerMemberId, reviewerDisplay),
        });
        return null;
    }

    /// <param name="verifiedActiveOwnerMemberIds">
    ///     Members whose source membership was verified as active client personnel for this command.
    /// </param>
    public CommandFailure? Approve(Uuid programId, long expectedRevision, Uuid approvalDecisionId,
        Uuid acceptedReviewDecisionId, DateOnly effectiveFrom, string rationale,
        IReadOnlySet<Uuid> verifiedActiveOwnerMemberIds, Uuid approverMemberId,
        string approverDisplay, DateTimeOffset decidedAt,
        SeparationOfDutiesWaiver? separationOfDutiesWaiver = null)
    {
        ArgumentNullException.ThrowIfNull(verifiedActiveOwnerMemberIds);
        if (CheckDecisionTarget(programId, expectedRevision) is { } target)
            return target;
        var scope = Scope(DraftVersionId, expectedRevision);
        if (CheckSeparationOfDuties(scope, approverMemberId, ResponsibilityType.PolicyApprover,
                SeparationOfDutiesActions.Approve, decidedAt, separationOfDutiesWaiver) is { } sod)
            return sod;
        if (acceptedReviewDecisionId == Uuid.Empty ||
            acceptedReviewDecisionId != _acceptedReviewDecisionId)
            return CommandFailure.StateConflict(
                "Approval requires the latest accepted review of this exact draft revision.");
        if (approvalDecisionId == Uuid.Empty || string.IsNullOrWhiteSpace(rationale) ||
            rationale.Length > 4000 || effectiveFrom == default)
            return CommandFailure.InvalidContent(
                "Approval requires an effective start date and a rationale of at most 4000 characters.");
        // Drafts recorded before a content rule existed must satisfy it before activation.
        if (ValidateContent(_currentContent) is { } invalid)
            return CommandFailure.InvalidContent(invalid.Message!);
        var owner = ActiveOwners(scope, decidedAt)
            .Where(assignment => verifiedActiveOwnerMemberIds.Contains(assignment.MemberId))
            .OrderBy(static assignment => assignment.AssignedAt)
            .ThenBy(static assignment => assignment.AssignmentId.ToString(), StringComparer.Ordinal)
            .FirstOrDefault();
        if (owner is null)
            return CommandFailure.StateConflict(
                "Activation requires an active client-personnel member assigned control_owner on this exact draft revision.");
        ControlApproved approved = new(_tenantId, ProgramId, Id, DraftVersionId, expectedRevision,
            approvalDecisionId, acceptedReviewDecisionId, _currentContent!, owner.AssignmentId,
            owner.MemberId, approverMemberId, approverDisplay, rationale.Trim(), effectiveFrom,
            decidedAt, separationOfDutiesWaiver?.Id)
        {
            StoredActor = ActorReference.ForMember(approverMemberId, approverDisplay),
        };
        if (JsonSerializer.SerializeToUtf8Bytes(approved,
                ComplianceCoreJsonContext.Default.ControlApproved).Length >
            MaximumDraftEventPayloadBytes)
            return CommandFailure.InvalidContent(
                "The control approval exceeds the bounded event payload size.");
        RaiseEvent(approved);
        return null;
    }

    static RequestError? Validate(string identifier, ControlDraftContent? content)
    {
        if (identifier.Length is < 1 or > 80 ||
            !identifier.All(static c => char.IsAsciiLetterUpper(c) ||
                char.IsAsciiDigit(c) || c is '-' or '_' or '.'))
            return new RequestError(RequestErrorKind.Validation,
                "A control identifier requires 1 to 80 ASCII letters, digits, hyphens, underscores, or periods.");
        return ValidateContent(content);
    }

    internal static RequestError? ValidateContent(ControlDraftContent? content)
    {
        if (content is null || content.ExpectedEvidenceDescriptions is null ||
            string.IsNullOrWhiteSpace(content.Title) ||
            string.IsNullOrWhiteSpace(content.Objective) ||
            string.IsNullOrWhiteSpace(content.Description) ||
            string.IsNullOrWhiteSpace(content.ImplementationNarrative))
            return new RequestError(RequestErrorKind.Validation,
                "A control draft requires title, objective, description, implementation narrative, and expected evidence descriptions.");
        if (content.Title.Length > 200 || content.Objective.Length > 2000 ||
            content.Description.Length > 8000 || content.ImplementationNarrative.Length > 12000 ||
            content.ExpectedEvidenceDescriptions.Count is < 1 or > 20 ||
            content.ExpectedEvidenceDescriptions.Any(static value =>
                string.IsNullOrWhiteSpace(value) || value.Length > 2000))
            return new RequestError(RequestErrorKind.Validation,
                "The control draft exceeds its content limits or has an empty expected evidence description.");
        if (content.OwnerReference is { Length: > 500 })
            return new RequestError(RequestErrorKind.Validation,
                "A control owner reference must be at most 500 characters.");
        var applicability = content.Applicability ?? [];
        if (applicability.Count > 100 || applicability.Any(static reference =>
                !IsValidApplicabilityReference(reference)) ||
            applicability.Select(static reference => reference.EntryId).Distinct().Count() !=
            applicability.Count)
            return new RequestError(RequestErrorKind.Validation,
            "Every control applicability reference requires a unique ID, typed subject, rationale, and a consistent governed reference state.");
        return null;
    }

    static bool IsValidApplicabilityReference(ControlApplicabilityReference? reference)
    {
        if (reference is null || reference.EntryId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(reference.Subject) || reference.Subject.Length > 500 ||
            string.IsNullOrWhiteSpace(reference.Rationale) || reference.Rationale.Length > 2000)
            return false;
        return reference.SubjectType switch
        {
            "application" or "system_instance" => !reference.Unresolved &&
                reference.GovernedRecordId is { } recordId && recordId != Uuid.Empty,
            "risk" or "process" => reference.Unresolved && reference.GovernedRecordId is null,
            _ => false,
        };
    }

    static ControlDraftContent Clean(ControlDraftContent content) => new(
        content.Title.Trim(), content.Objective.Trim(), content.Description.Trim(),
        content.ImplementationNarrative.Trim(),
        content.ExpectedEvidenceDescriptions.Select(static value => value.Trim()).ToArray(),
        string.IsNullOrWhiteSpace(content.OwnerReference) ? null : content.OwnerReference.Trim(),
        (content.Applicability ?? []).Select(static reference => new ControlApplicabilityReference(
            reference.EntryId, reference.SubjectType, reference.Subject.Trim(),
            reference.GovernedRecordId, reference.Rationale.Trim(), reference.Unresolved)).ToArray());

    static bool Equal(ControlDraftContent left, ControlDraftContent right) =>
        StringComparer.Ordinal.Equals(left.Title, right.Title) &&
        StringComparer.Ordinal.Equals(left.Objective, right.Objective) &&
        StringComparer.Ordinal.Equals(left.Description, right.Description) &&
        StringComparer.Ordinal.Equals(left.ImplementationNarrative, right.ImplementationNarrative) &&
        left.ExpectedEvidenceDescriptions.SequenceEqual(right.ExpectedEvidenceDescriptions,
            StringComparer.Ordinal) &&
        StringComparer.Ordinal.Equals(left.OwnerReference, right.OwnerReference) &&
        (left.Applicability ?? []).SequenceEqual(right.Applicability ?? []);
}
