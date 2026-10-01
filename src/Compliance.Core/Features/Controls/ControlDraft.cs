using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     The Control record: its draft line, immutable approved versions with effective intervals,
///     successor and retirement proposals, and every review and approval decision.
/// </summary>
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
    bool _responsibilityEverAssigned;
    Uuid _acceptedReviewDecisionId;
    Uuid? _latestReviewDecisionId;
    Uuid? _draftVersionId;
    Uuid? _draftPredecessorVersionId;
    PendingRetirement? _pendingRetirement;
    bool _retired;
    readonly List<ControlVersionView> _versions = [];
    readonly List<ControlDecisionView> _decisions = [];
    readonly Dictionary<ResponsibilityScope, ResponsibilitySet> _responsibilitySets = [];
    readonly Dictionary<ResponsibilityScope, List<ResponsibilityDecisionFact>> _responsibilityDecisions = [];

    public bool IsCreated => _created;
    public Uuid TenantId => _tenantId;

    /// <summary>
    ///     The open draft's version line, or the latest approved version when no draft is open.
    /// </summary>
    public Uuid DraftVersionId => _draftVersionId ?? _versions.LastOrDefault()?.VersionId ??
        ControlVersionIds.Initial(Id);

    public bool HasOpenDraft => IsVisible && !_retired && _draftVersionId is not null;
    public bool IsApproved => _versions.Count > 0;
    public bool IsRetired => _retired;
    public Uuid? PendingRetirementId => _pendingRetirement?.RetirementId;
    public DateOnly? PendingRetirementEffectiveUntil => _pendingRetirement?.EffectiveUntil;
    public ControlDraftContent? CurrentContent => _currentContent;

    /// <summary>The latest approved version, including a superseding or retired one.</summary>
    public ControlVersionView? ApprovedVersion => _versions.LastOrDefault();

    public IReadOnlyList<ControlVersionView> ReadVersions() => _versions.ToArray();
    public IReadOnlyList<ControlDecisionView> ReadDecisions() => _decisions.ToArray();
    public bool IsVisible => _created && !_discarded;
    public long Revision => _revision;
    public Uuid ProgramId { get; private set; }

    /// <summary>The pending decision target: an open draft version or a retirement proposal.</summary>
    public Uuid? PendingTargetId => !IsVisible || _retired
        ? null
        : _draftVersionId ?? _pendingRetirement?.RetirementId;

    /// <summary>The approved version whose half-open effective interval contains the date.</summary>
    public ControlVersionView? EffectiveVersion(DateOnly date) => _versions.LastOrDefault(
        version => new EffectiveInterval(version.EffectiveFrom, version.EffectiveUntil)
            .Contains(date));

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
            _draftVersionId = ControlVersionIds.Initial(Id);
        });
        On<ControlSuccessorProposed>(ev =>
        {
            _draftVersionId = ev.VersionId;
            _draftPredecessorVersionId = ev.PredecessorVersionId;
            _draftAuthorMemberId = ev.ActorMemberId;
            _pendingRetirement = null;
            _acceptedReviewDecisionId = Uuid.Empty;
            _latestReviewDecisionId = null;
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
            if (ev.PredecessorVersionId is { } predecessorId)
            {
                var index = _versions.FindIndex(version => version.VersionId == predecessorId);
                if (index >= 0)
                    _versions[index] = _versions[index] with
                    {
                        Status = "superseded",
                        EffectiveUntil = ev.EffectiveFrom,
                    };
            }
            _versions.Add(new ControlVersionView(ev.TenantId, ev.ProgramId, ev.ControlId,
                _identifier!, ev.VersionId, ev.Revision, "approved", "organization_authored",
                ev.Content, ev.EffectiveFrom, ev.PredecessorVersionId, ev.OwnerAssignmentId,
                ev.OwnerMemberId, "verified_member", ev.AcceptedReviewDecisionId,
                ev.ApprovalDecisionId, ev.Actor, ev.Rationale, ev.DecidedAt,
                ev.SeparationOfDutiesWaiverId));
            _acceptedReviewDecisionId = Uuid.Empty;
            _draftVersionId = null;
            _draftPredecessorVersionId = null;
        });
        On<ControlRetirementProposed>(ev =>
        {
            _pendingRetirement = new PendingRetirement(ev.RetirementId, ev.VersionId,
                ev.EffectiveUntil);
            _draftAuthorMemberId = ev.ActorMemberId;
            _acceptedReviewDecisionId = Uuid.Empty;
            _latestReviewDecisionId = null;
        });
        On<ControlRetired>(ev =>
        {
            _decisions.Add(new ControlDecisionView(ev.TenantId, ev.ProgramId, ev.ControlId,
                ev.DecisionId, ev.RetirementId, ev.Revision, "retirement", "retire", ev.Actor,
                ev.Rationale, ev.DecidedAt, null, ev.AcceptedReviewDecisionId,
                ev.SeparationOfDutiesWaiverId));
            RecordResponsibilityDecision(Scope(ev.RetirementId, ev.Revision), ev.ActorMemberId,
                ResponsibilityType.PolicyApprover, ev.DecidedAt, ev.SeparationOfDutiesWaiverId);
            var index = _versions.FindIndex(version => version.VersionId == ev.VersionId);
            if (index >= 0)
                _versions[index] = _versions[index] with
                {
                    Status = "retired",
                    EffectiveUntil = ev.EffectiveUntil,
                };
            _retired = true;
            _pendingRetirement = null;
            _acceptedReviewDecisionId = Uuid.Empty;
        });
        On<ResponsibilityAssigned>(ev =>
        {
            _responsibilityEverAssigned = true;
            GetResponsibilitySet(ev.Scope).Apply(ev);
        });
        On<ResponsibilityRevoked>(ev => GetResponsibilitySet(ev.Scope).Apply(ev));
        On<ControlDraftDiscarded>(ev =>
        {
            _revision = ev.Revision;
            _discarded = true;
        });
    }

    sealed record PendingRetirement(Uuid RetirementId, Uuid VersionId, DateOnly EffectiveUntil);

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
        if (_retired)
            return CommandFailure.StateConflict("A retired control cannot be revised.");
        if (!HasOpenDraft)
            return CommandFailure.StateConflict(
                "The approved control version is immutable. Propose a successor draft.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("control draft",
                _revision));
        var error = Validate(_identifier!, content);
        if (error is not null)
            return CommandFailure.InvalidContent(error.Message!);
        return RaiseRevision(programId, content, actorMemberId, actorDisplay, changedAt);
    }

    CommandFailure? RaiseRevision(Uuid programId, ControlDraftContent content,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt,
        ControlSuccessorProposed? proposal = null)
    {
        ControlDraftRevised revised = new(_tenantId, programId, Id, _revision + 1,
            Clean(content), actorMemberId, actorDisplay, changedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        };
        if (JsonSerializer.SerializeToUtf8Bytes(revised,
                ComplianceCoreJsonContext.Default.ControlDraftRevised).Length >
            MaximumDraftEventPayloadBytes)
            return CommandFailure.InvalidContent("The control draft exceeds the bounded event payload size.");
        if (proposal is not null)
            RaiseEvent(proposal);
        RaiseEvent(revised);
        return null;
    }

    /// <summary>
    ///     Opens a successor draft from the exact current approved version. The predecessor stays
    ///     effective and immutable until the successor is independently reviewed and approved.
    /// </summary>
    public CommandFailure? ProposeSuccessor(Uuid programId, Uuid expectedApprovedVersionId,
        ControlDraftContent content, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt)
    {
        if (!IsVisible || ProgramId != programId)
            return CommandFailure.MissingRecord("The control was not found.");
        if (_retired)
            return CommandFailure.StateConflict("A retired control cannot have a successor.");
        if (ApprovedVersion is not { } current)
            return CommandFailure.StateConflict(
                "The control has no approved version. Revise its initial draft instead.");
        if (expectedApprovedVersionId != current.VersionId)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleApprovedVersion("control",
                current.VersionId.ToGuid()));
        if (HasOpenDraft)
            return CommandFailure.StateConflict("The control already has an open successor draft.");
        var error = Validate(_identifier!, content);
        if (error is not null)
            return CommandFailure.InvalidContent(error.Message!);
        var proposal = new ControlSuccessorProposed(_tenantId, programId, Id,
            ControlVersionIds.Sequence(Id, _versions.Count + 1), current.VersionId,
            _revision + 1, actorMemberId, actorDisplay, changedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        };
        return RaiseRevision(programId, content, actorMemberId, actorDisplay, changedAt,
            proposal);
    }

    /// <summary>Proposes ending future use of the exact current approved version.</summary>
    public CommandFailure? ProposeRetirement(Uuid programId, Uuid expectedApprovedVersionId,
        DateOnly effectiveUntil, string rationale, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt)
    {
        if (!IsVisible || ProgramId != programId)
            return CommandFailure.MissingRecord("The control was not found.");
        if (_retired)
            return CommandFailure.StateConflict("The control is already retired.");
        if (ApprovedVersion is not { } current)
            return CommandFailure.StateConflict(
                "Only an approved control can be retired. Discard an unused draft instead.");
        if (expectedApprovedVersionId != current.VersionId)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleApprovedVersion("control",
                current.VersionId.ToGuid()));
        if (HasOpenDraft)
            return CommandFailure.StateConflict(
                "The control has an open successor draft. Approve it before proposing retirement.");
        if (_pendingRetirement is not null)
            return CommandFailure.StateConflict("The control already has a pending retirement.");
        if (effectiveUntil == default || !EffectiveInterval.CanFollow(current.EffectiveFrom,
                effectiveUntil))
            return CommandFailure.InvalidContent(
                "Retirement must take effect after the current version's effective start.");
        if (string.IsNullOrWhiteSpace(rationale) || rationale.Length > 4000)
            return CommandFailure.InvalidContent(
                "Retirement requires a rationale of at most 4000 characters.");
        RaiseEvent(new ControlRetirementProposed(_tenantId, programId, Id,
            ControlVersionIds.Retirement(Id, current.VersionId), current.VersionId, _revision,
            effectiveUntil, rationale.Trim(), actorMemberId, actorDisplay, changedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return null;
    }

    public CommandFailure? Discard(Uuid programId, long expectedRevision, string rationale,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset discardedAt)
    {
        if (!_created || _discarded || ProgramId != programId)
            return CommandFailure.MissingRecord("The control draft was not found.");
        if (IsApproved)
            return CommandFailure.StateConflict(
                "An approved, superseded, or retired control cannot be discarded. Propose retirement instead.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("control draft",
                _revision));
        if (VersionedRecordRules.DraftDiscardConflict(_everReviewed) is { } referenced)
            return CommandFailure.ForVersion(referenced);
        if (_currentContent?.Applicability is { Count: > 0 })
            return CommandFailure.StateConflict(
                "Discarding a control draft requires no retained applicability relationships.");
        // Responsibilities are retained relationships even after revocation; checking the
        // authoritative stream means a caller's read access cannot make deletion appear safe.
        if (_responsibilityEverAssigned)
            return CommandFailure.StateConflict(
                "Discarding a control draft requires that no responsibility was ever assigned to it.");
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

    /// <summary>Unrevoked responsibilities on an approved version's or pending target's scope.</summary>
    public IReadOnlyList<ResponsibilityAssignmentView> RetainedResponsibilities(Uuid versionId,
        long revision) => GetResponsibilitySet(Scope(versionId, revision)).ReadAssignments()
        .Where(static assignment => assignment.RevokedAt is null)
        .ToArray();

    /// <summary>Whether the scope is the exact current pending draft or retirement revision.</summary>
    public bool IsCurrentResponsibilityScope(ResponsibilityScope scope) =>
        scope is not null &&
        StringComparer.Ordinal.Equals(scope.RecordType, SeparationOfDutiesRecordTypes.Control) &&
        scope.RecordId == Id && PendingTargetId is { } target && scope.VersionId == target &&
        scope.Revision == _revision;

    /// <summary>Members holding a control_owner responsibility on the current exact revision.</summary>
    public IReadOnlyList<Uuid> CurrentOwnerMemberIds(DateTimeOffset at) =>
        !HasOpenDraft
            ? []
            : ActiveOwners(Scope(_draftVersionId!.Value, _revision), at)
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
                "Responsibilities must target the current exact pending control revision.");
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
                "Responsibilities must target the current exact pending control revision.");
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
        if (_retired)
            return CommandFailure.StateConflict("The control is retired.");
        if (PendingTargetId is null)
            return CommandFailure.StateConflict(
                "The control version is already approved and has no pending successor or retirement.");
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
                "A control draft or retirement author cannot " + action + " their own proposal.")
            : null;
    }

    public CommandFailure? Review(Uuid programId, long expectedRevision, Uuid decisionId,
        string outcome, string rationale, Uuid reviewerMemberId, string reviewerDisplay,
        DateTimeOffset decidedAt, SeparationOfDutiesWaiver? separationOfDutiesWaiver = null)
    {
        if (CheckDecisionTarget(programId, expectedRevision) is { } target)
            return target;
        var targetId = PendingTargetId!.Value;
        var scope = Scope(targetId, expectedRevision);
        if (CheckSeparationOfDuties(scope, reviewerMemberId, ResponsibilityType.AssignedReviewer,
                SeparationOfDutiesActions.Review, decidedAt, separationOfDutiesWaiver) is { } sod)
            return sod;
        if (decisionId == Uuid.Empty || outcome is not ("accept" or "request_changes") ||
            string.IsNullOrWhiteSpace(rationale) || rationale.Length > 4000)
            return CommandFailure.InvalidContent(
                "A control review requires an outcome of accept or request_changes and a rationale of at most 4000 characters.");
        RaiseEvent(new ControlReviewed(_tenantId, ProgramId, Id, targetId,
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
    /// <param name="impactDigest">
    ///     The acknowledged impact preview digest; required when approving a successor.
    /// </param>
    public CommandFailure? Approve(Uuid programId, long expectedRevision, Uuid approvalDecisionId,
        Uuid acceptedReviewDecisionId, DateOnly effectiveFrom, string rationale,
        IReadOnlySet<Uuid> verifiedActiveOwnerMemberIds, Uuid approverMemberId,
        string approverDisplay, DateTimeOffset decidedAt,
        SeparationOfDutiesWaiver? separationOfDutiesWaiver = null, string? impactDigest = null)
    {
        ArgumentNullException.ThrowIfNull(verifiedActiveOwnerMemberIds);
        if (CheckDecisionTarget(programId, expectedRevision) is { } target)
            return target;
        if (!HasOpenDraft)
            return CommandFailure.StateConflict(
                "Only an open control draft can be approved. Retirement uses its own decision.");
        var draftVersionId = _draftVersionId!.Value;
        var scope = Scope(draftVersionId, expectedRevision);
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
        var predecessor = _draftPredecessorVersionId is { } predecessorId
            ? _versions.Single(version => version.VersionId == predecessorId)
            : null;
        if (predecessor is not null)
        {
            if (string.IsNullOrWhiteSpace(impactDigest))
                return CommandFailure.InvalidContent(
                    "Successor approval requires the acknowledged impact preview digest.");
            if (!EffectiveInterval.CanFollow(predecessor.EffectiveFrom, effectiveFrom))
                return CommandFailure.InvalidContent(
                    "A successor must become effective after its predecessor's effective start.");
        }
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
        ControlApproved approved = new(_tenantId, ProgramId, Id, draftVersionId, expectedRevision,
            approvalDecisionId, acceptedReviewDecisionId, _currentContent!, owner.AssignmentId,
            owner.MemberId, approverMemberId, approverDisplay, rationale.Trim(), effectiveFrom,
            decidedAt, separationOfDutiesWaiver?.Id, predecessor?.VersionId,
            predecessor is null ? null : impactDigest)
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

    /// <summary>
    ///     Approves the pending retirement of the exact current version after an independent
    ///     accepted review. History and prior effective intervals are retained.
    /// </summary>
    public CommandFailure? Retire(Uuid programId, long expectedRevision, Uuid decisionId,
        Uuid acceptedReviewDecisionId, string impactDigest, string rationale,
        Uuid approverMemberId, string approverDisplay, DateTimeOffset decidedAt,
        SeparationOfDutiesWaiver? separationOfDutiesWaiver = null)
    {
        if (CheckDecisionTarget(programId, expectedRevision) is { } target)
            return target;
        if (_pendingRetirement is not { } pending)
            return CommandFailure.StateConflict("The control has no pending retirement.");
        var scope = Scope(pending.RetirementId, expectedRevision);
        if (CheckSeparationOfDuties(scope, approverMemberId, ResponsibilityType.PolicyApprover,
                SeparationOfDutiesActions.Approve, decidedAt, separationOfDutiesWaiver) is { } sod)
            return sod;
        if (acceptedReviewDecisionId == Uuid.Empty ||
            acceptedReviewDecisionId != _acceptedReviewDecisionId)
            return CommandFailure.StateConflict(
                "Retirement requires the latest accepted review of this exact retirement proposal.");
        if (decisionId == Uuid.Empty || string.IsNullOrWhiteSpace(rationale) ||
            rationale.Length > 4000)
            return CommandFailure.InvalidContent(
                "Retirement approval requires a rationale of at most 4000 characters.");
        if (string.IsNullOrWhiteSpace(impactDigest))
            return CommandFailure.InvalidContent(
                "Retirement approval requires the acknowledged impact preview digest.");
        RaiseEvent(new ControlRetired(_tenantId, ProgramId, Id, pending.RetirementId,
            pending.VersionId, expectedRevision, decisionId, acceptedReviewDecisionId,
            pending.EffectiveUntil, impactDigest, approverMemberId, approverDisplay,
            rationale.Trim(), decidedAt, separationOfDutiesWaiver?.Id)
        {
            StoredActor = ActorReference.ForMember(approverMemberId, approverDisplay),
        });
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
            "application" or "system_instance" or "commitment" => !reference.Unresolved &&
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
