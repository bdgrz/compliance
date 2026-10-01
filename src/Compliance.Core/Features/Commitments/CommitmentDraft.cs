using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class CommitmentDraft : Aggregate
{
    // Fitz stream frames are u16; leave room for the Portia envelope and metadata.
    public const int MaximumDraftEventPayloadBytes = 48 * 1024;
    static readonly HashSet<string> AllowedKinds = new(StringComparer.Ordinal)
    {
        "service_commitment", "system_requirement", "user_entity_responsibility",
        "subservice_responsibility",
    };

    readonly Uuid _tenantId;
    bool _created;
    long _revision;
    Uuid _createRequestId;
    string? _initialStatement;
    string? _initialContext;
    string? _initialSourceReference;
    readonly HashSet<Uuid> _pendingAuthors = [];
    readonly List<(long Version, long Revision, DateOnly EffectiveFrom)> _versions = [];
    readonly Dictionary<ResponsibilityScope, ResponsibilitySet> _responsibilitySets = [];
    readonly Dictionary<ResponsibilityScope, List<ResponsibilityDecisionFact>> _responsibilityDecisions = [];
    string? _sourceReference;
    AcceptedReview? _acceptedReview;

    public bool IsCreated => _created;
    public string? SourceReference => _sourceReference;

    /// <summary>The accepted review of the current revision that is waiting for approval.</summary>
    public Uuid? AcceptedReviewDecisionId => _acceptedReview?.DecisionId;
    public long Revision => _revision;
    public Uuid ProgramId { get; private set; }
    public Uuid ServiceId { get; private set; }
    public string? Kind { get; private set; }
    public string? Identifier { get; private set; }
    public long EffectiveVersionCount => _versions.Count;
    public long? LatestEffectiveRevision => _versions.Count == 0 ? null : _versions[^1].Revision;

    /// <summary>The effective version on a date, or null before the first effective date.</summary>
    public long? EffectiveVersionOn(DateOnly date)
    {
        for (var index = _versions.Count - 1; index >= 0; index--)
            if (_versions[index].EffectiveFrom <= date)
                return _versions[index].Version;
        return null;
    }

    public CommitmentDraft(Uuid tenantId, Uuid draftId)
        : base(draftId, new EventStreamAddress(tenantId.ToString(), "commitment-drafts",
            draftId.ToString()))
    {
        _tenantId = tenantId;
        On<CommitmentDraftCreated>(ev =>
        {
            _created = true;
            _revision = 1;
            ProgramId = ev.ProgramId;
            ServiceId = ev.ServiceId;
            Kind = ev.Kind;
            Identifier = ev.Identifier;
            _createRequestId = ev.CreateRequestId;
            _initialStatement = ev.Statement;
            _initialContext = ev.Context;
            _initialSourceReference = ev.SourceReference;
            _sourceReference = ev.SourceReference;
            _pendingAuthors.Add(ev.ActorMemberId);
        });
        On<CommitmentDraftRevised>(ev =>
        {
            _revision = ev.Revision;
            _sourceReference = ev.SourceReference;
            _acceptedReview = null;
            if (LatestEffectiveRevision is { } effective && effective == ev.Revision - 1)
                _pendingAuthors.Clear();
            _pendingAuthors.Add(ev.ActorMemberId);
        });
        On<CommitmentReviewed>(ev =>
        {
            RecordResponsibilityDecision(Scope(ev.Revision), ev.ActorMemberId,
                ResponsibilityType.AssignedReviewer, ev.DecidedAt, ev.SeparationOfDutiesWaiverId);
            // Legacy reviews created the version directly; newer accepted reviews await approval.
            if (ev.Version is { } version && ev.EffectiveFrom is { } effectiveFrom)
            {
                _versions.Add((version, ev.Revision, effectiveFrom));
                _acceptedReview = null;
            }
            else
                _acceptedReview = StringComparer.Ordinal.Equals(ev.Outcome, "accept")
                    ? new AcceptedReview(ev.DecisionId, ev.Revision, ev.ActorMemberId)
                    : null;
        });
        On<CommitmentApproved>(ev =>
        {
            RecordResponsibilityDecision(Scope(ev.Revision), ev.ActorMemberId,
                ResponsibilityType.PolicyApprover, ev.DecidedAt, ev.SeparationOfDutiesWaiverId);
            _versions.Add((ev.Version, ev.Revision, ev.EffectiveFrom));
            _acceptedReview = null;
        });
        On<ResponsibilityAssigned>(ev => GetResponsibilitySet(ev.Scope).Apply(ev));
        On<ResponsibilityRevoked>(ev => GetResponsibilitySet(ev.Scope).Apply(ev));
    }

    public static string NormalizeIdentifier(string? identifier) =>
        identifier?.Trim().ToUpperInvariant() ?? string.Empty;

    public static string NormalizeKind(string? kind) => kind?.Trim().ToLowerInvariant() ?? string.Empty;

    public static Uuid IdFor(Uuid tenantId, Uuid programId, string kind, string identifier) =>
        Uuid.CreateVersion5(Uuid.CreateVersion5(tenantId, programId.ToString()),
            $"{NormalizeKind(kind)}:{NormalizeIdentifier(identifier)}");

    public Result<CommitmentDraftRegistration> Create(Uuid programId, Uuid createRequestId,
        Uuid serviceId, string kind, string identifier, string statement, string context,
        string sourceReference, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt)
    {
        var normalizedKind = NormalizeKind(kind);
        var normalizedIdentifier = NormalizeIdentifier(identifier);
        var error = Validate(normalizedKind, normalizedIdentifier, statement, context,
            sourceReference);
        if (error is not null)
            return Result<CommitmentDraftRegistration>.Failure(error);
        if (serviceId == Uuid.Empty)
            return Result<CommitmentDraftRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation, "A draft requires a governed service."));
        var cleanStatement = statement.Trim();
        var cleanContext = context.Trim();
        var cleanSourceReference = sourceReference.Trim();
        if (_created)
            return ProgramId == programId && ServiceId == serviceId &&
                   _createRequestId == createRequestId &&
                   StringComparer.Ordinal.Equals(Kind, normalizedKind) &&
                   StringComparer.Ordinal.Equals(Identifier, normalizedIdentifier) &&
                   StringComparer.Ordinal.Equals(_initialStatement, cleanStatement) &&
                   StringComparer.Ordinal.Equals(_initialContext, cleanContext) &&
                   StringComparer.Ordinal.Equals(_initialSourceReference, cleanSourceReference)
                ? Result<CommitmentDraftRegistration>.Success(new CommitmentDraftRegistration(
                    Id, normalizedKind, normalizedIdentifier, 1))
                : Result<CommitmentDraftRegistration>.Failure(new RequestError(
                    RequestErrorKind.Conflict, "The draft identifier already exists."));
        CommitmentDraftCreated created = new(_tenantId, programId, Id, createRequestId,
            serviceId, normalizedKind, normalizedIdentifier, cleanStatement, cleanContext,
            cleanSourceReference, actorMemberId, actorDisplay, changedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        };
        if (JsonSerializer.SerializeToUtf8Bytes(created,
                ComplianceCoreJsonContext.Default.CommitmentDraftCreated).Length >
            MaximumDraftEventPayloadBytes)
            return Result<CommitmentDraftRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation, "The draft exceeds the bounded event payload size."));
        RaiseEvent(created);
        return Result<CommitmentDraftRegistration>.Success(new CommitmentDraftRegistration(
            Id, normalizedKind, normalizedIdentifier, 1));
    }

    public CommandFailure? Revise(Uuid programId, long expectedRevision, string statement,
        string context, string sourceReference, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt)
    {
        if (!_created || ProgramId != programId)
            return CommandFailure.MissingRecord("The draft was not found.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "commitment draft", _revision));
        var error = Validate(Kind!, Identifier!, statement, context, sourceReference);
        if (error is not null)
            return CommandFailure.InvalidContent(error.Message!);
        CommitmentDraftRevised revised = new(_tenantId, programId, Id, _revision + 1,
            statement.Trim(), context.Trim(), sourceReference.Trim(), actorMemberId,
            actorDisplay, changedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        };
        if (JsonSerializer.SerializeToUtf8Bytes(revised,
                ComplianceCoreJsonContext.Default.CommitmentDraftRevised).Length >
            MaximumDraftEventPayloadBytes)
            return CommandFailure.InvalidContent("The draft exceeds the bounded event payload size.");
        RaiseEvent(revised);
        return null;
    }

    /// <summary>The party that performs a commitment of this kind.</summary>
    public static string PerformedBy(string kind) => kind switch
    {
        "user_entity_responsibility" => "user_entity",
        "subservice_responsibility" => "subservice_organization",
        _ => "service_organization",
    };

    /// <summary>CUECs and CSOCs are never internally performed.</summary>
    public static bool IsInternallyPerformed(string kind) =>
        StringComparer.Ordinal.Equals(PerformedBy(kind), "service_organization");

    public CommandFailure? Review(Uuid programId, long expectedRevision, Uuid decisionId,
        string outcome, string? ownerReference, string? applicability, string? interpretation,
        string? interpretationNote, string rationale, Uuid reviewerMemberId,
        string reviewerDisplay, DateTimeOffset decidedAt,
        SeparationOfDutiesWaiver? separationOfDutiesWaiver = null,
        string? sourceVerifiedReference = null, string? sourceEvidence = null)
    {
        if (CheckTarget(programId, expectedRevision) is { } target)
            return target;
        if (CheckSeparationOfDuties(expectedRevision, reviewerMemberId,
                ResponsibilityType.AssignedReviewer, SeparationOfDutiesActions.Review, decidedAt,
                separationOfDutiesWaiver, false) is { } sod)
            return sod;
        if (decisionId == Uuid.Empty || string.IsNullOrWhiteSpace(rationale) ||
            rationale.Length > 4000 || interpretationNote?.Length > 4000)
            return CommandFailure.InvalidContent("A review requires a bounded rationale.");
        string? sourceVerification = null;
        switch (outcome)
        {
            case "request_changes":
                break;
            case "accept":
                if (string.IsNullOrWhiteSpace(ownerReference) || ownerReference.Length > 200)
                    return CommandFailure.InvalidContent("Acceptance requires a verified owner.");
                if (applicability is not ("applicable" or "not_applicable"))
                    return CommandFailure.InvalidContent(
                        "Applicability must be applicable or not_applicable.");
                if (interpretation is not ("supported" or "unsupported"))
                    return CommandFailure.InvalidContent(
                        "Interpretation must be supported or unsupported.");
                if (string.IsNullOrWhiteSpace(sourceVerifiedReference) ||
                    !StringComparer.Ordinal.Equals(sourceVerifiedReference.Trim(), _sourceReference))
                    return CommandFailure.InvalidContent(
                        "Acceptance requires verifying the draft's exact current source reference.");
                if (string.IsNullOrWhiteSpace(sourceEvidence) || sourceEvidence.Length > 2000)
                    return CommandFailure.InvalidContent(
                        "Acceptance requires bounded evidence of the source that was checked.");
                if (LatestEffectiveRevision == _revision)
                    return CommandFailure.StateConflict(
                        "This revision is already effective. Revise the draft to propose a successor.");
                sourceVerification = "verified";
                break;
            default:
                return CommandFailure.InvalidContent("The outcome must be accept or request_changes.");
        }
        var accepted = sourceVerification is not null;
        RaiseEvent(new CommitmentReviewed(_tenantId, programId, Id, expectedRevision,
            decisionId, outcome, accepted ? ownerReference!.Trim() : null,
            accepted ? applicability : null, accepted ? interpretation : null,
            string.IsNullOrWhiteSpace(interpretationNote) ? null : interpretationNote.Trim(),
            rationale.Trim(), null, null, null, reviewerMemberId, reviewerDisplay, decidedAt,
            separationOfDutiesWaiver?.Id, sourceVerification,
            accepted ? sourceVerifiedReference!.Trim() : null,
            accepted ? sourceEvidence!.Trim() : null)
        {
            StoredActor = ActorReference.ForMember(reviewerMemberId, reviewerDisplay),
        });
        return null;
    }

    /// <summary>Approves the accepted review of the exact revision into an effective version.</summary>
    public CommandFailure? Approve(Uuid programId, long expectedRevision, Uuid decisionId,
        Uuid acceptedReviewDecisionId, DateOnly effectiveFrom, string rationale,
        string impactDigest, Uuid approverMemberId, string approverDisplay,
        DateTimeOffset decidedAt, SeparationOfDutiesWaiver? separationOfDutiesWaiver = null)
    {
        if (CheckTarget(programId, expectedRevision) is { } target)
            return target;
        if (_acceptedReview is not { } review || review.Revision != _revision ||
            acceptedReviewDecisionId == Uuid.Empty || acceptedReviewDecisionId != review.DecisionId)
            return CommandFailure.StateConflict(
                "Approval requires the latest accepted review of this exact draft revision.");
        if (CheckSeparationOfDuties(expectedRevision, approverMemberId,
                ResponsibilityType.PolicyApprover, SeparationOfDutiesActions.Approve, decidedAt,
                separationOfDutiesWaiver, approverMemberId == review.ReviewerMemberId) is { } sod)
            return sod;
        if (decisionId == Uuid.Empty || string.IsNullOrWhiteSpace(rationale) ||
            rationale.Length > 4000 || effectiveFrom == default)
            return CommandFailure.InvalidContent(
                "Approval requires an effective start date and a rationale of at most 4000 characters.");
        if (string.IsNullOrWhiteSpace(impactDigest))
            return CommandFailure.InvalidContent(
                "Approval requires the acknowledged impact preview digest.");
        if (_versions.Count > 0 && effectiveFrom <= _versions[^1].EffectiveFrom)
            return CommandFailure.InvalidContent(
                "A successor must become effective after the previous effective version.");
        RaiseEvent(new CommitmentApproved(_tenantId, programId, Id, expectedRevision, decisionId,
            review.DecisionId, _versions.Count + 1, effectiveFrom, impactDigest,
            rationale.Trim(), approverMemberId, approverDisplay, decidedAt,
            separationOfDutiesWaiver?.Id)
        {
            StoredActor = ActorReference.ForMember(approverMemberId, approverDisplay),
        });
        return null;
    }

    CommandFailure? CheckTarget(Uuid programId, long expectedRevision)
    {
        if (!_created || ProgramId != programId)
            return CommandFailure.MissingRecord("The draft was not found.");
        return expectedRevision != _revision
            ? CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("commitment draft",
                _revision))
            : null;
    }

    /// <summary>
    ///     Authors since the last effective version may not review or approve, an approver may not be
    ///     the accepted reviewer, and conflicting responsibilities block the decision, unless an
    ///     approved waiver names the exact scope, action, and member. When a revision has active
    ///     assignments of the deciding responsibility, only those assignees may decide.
    /// </summary>
    CommandFailure? CheckSeparationOfDuties(long revision, Uuid memberId,
        ResponsibilityType decisionType, string action, DateTimeOffset at,
        SeparationOfDutiesWaiver? waiver, bool isAcceptedReviewer)
    {
        var scope = Scope(revision);
        var isAuthor = _pendingAuthors.Contains(memberId);
        var set = GetResponsibilitySet(scope);
        var assigned = set.ReadAssignments().Where(assignment => assignment.Type == decisionType &&
            assignment.RevokedAt is null && assignment.EffectiveFrom <= at &&
            (assignment.EffectiveUntil is null || assignment.EffectiveUntil > at)).ToArray();
        if (assigned.Length > 0 && assigned.All(assignment => assignment.MemberId != memberId))
            return CommandFailure.ActorProhibited(decisionType == ResponsibilityType.AssignedReviewer
                ? "Only an assigned reviewer of this exact revision can review it."
                : "Only an assigned approver of this exact revision can approve it.");
        var failure = ResponsibilityDecisionGuard.Validate(set, scope, memberId, decisionType, at,
            isAuthor || isAcceptedReviewer, waiver);
        if (failure is not null)
            return failure;
        var waiverScope = new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.Commitment,
            Id, Id, revision, action);
        if (waiver is not null &&
            (waiver.TenantId != _tenantId || !waiver.Allows(waiverScope, memberId, at)))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this member and draft revision.");
        if (waiver is not null)
            return null;
        if (isAuthor)
            return CommandFailure.ActorProhibited(
                "A commitment author cannot " + action + " a revision they authored.");
        return isAcceptedReviewer
            ? CommandFailure.ActorProhibited(
                "The accepted reviewer cannot also approve the same revision.")
            : null;
    }

    ResponsibilityScope Scope(long revision) =>
        new(SeparationOfDutiesRecordTypes.Commitment, Id, Id, revision);

    public ResponsibilitySet GetResponsibilitySet(ResponsibilityScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (!StringComparer.Ordinal.Equals(scope.RecordType, SeparationOfDutiesRecordTypes.Commitment) ||
            scope.RecordId != Id)
            throw new ArgumentException("The responsibility scope must belong to this commitment.",
                nameof(scope));
        if (!_responsibilitySets.TryGetValue(scope, out var set))
        {
            set = new ResponsibilitySet(_tenantId, scope);
            _responsibilitySets.Add(scope, set);
        }
        return set;
    }

    /// <summary>Responsibilities target the current exact draft revision; the version ID is the draft ID.</summary>
    public bool IsCurrentResponsibilityScope(ResponsibilityScope scope) =>
        scope is not null && _created &&
        StringComparer.Ordinal.Equals(scope.RecordType, SeparationOfDutiesRecordTypes.Commitment) &&
        scope.RecordId == Id && scope.VersionId == Id && scope.Revision == _revision &&
        LatestEffectiveRevision != _revision;

    public CommandFailure? AssignResponsibility(ResponsibilityScope scope, Uuid assignmentId,
        Uuid memberId, ResponsibilityType type, Uuid assignedByMemberId,
        string assignedByDisplay, DateTimeOffset assignedAt, DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveUntil, IReadOnlyList<SeparationOfDutiesWaiver> waivers)
    {
        if (!IsCurrentResponsibilityScope(scope))
            return CommandFailure.StateConflict(
                "Responsibilities must target the current exact pending commitment revision.");
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
                "Responsibilities must target the current exact pending commitment revision.");
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

    sealed record AcceptedReview(Uuid DecisionId, long Revision, Uuid ReviewerMemberId);

    sealed record ResponsibilityDecisionFact(Uuid MemberId, ResponsibilityType Type,
        DateTimeOffset At, Uuid? WaiverId);

    static RequestError? Validate(string kind, string identifier, string statement,
        string context, string sourceReference)
    {
        if (!AllowedKinds.Contains(kind))
            return new RequestError(RequestErrorKind.Validation,
                "The draft kind is unsupported.");
        if (identifier.Length is < 1 or > 80 ||
            !identifier.All(static c => char.IsAsciiLetterUpper(c) ||
                char.IsAsciiDigit(c) || c is '-' or '_' or '.'))
            return new RequestError(RequestErrorKind.Validation,
                "An identifier requires 1 to 80 ASCII letters, digits, hyphens, underscores, or periods.");
        if (string.IsNullOrWhiteSpace(statement) || statement.Length > 16000 ||
            context is null || context.Length > 16000 ||
            string.IsNullOrWhiteSpace(sourceReference) || sourceReference.Length > 1024)
            return new RequestError(RequestErrorKind.Validation,
                "A draft requires a bounded statement and source reference.");
        return null;
    }
}
