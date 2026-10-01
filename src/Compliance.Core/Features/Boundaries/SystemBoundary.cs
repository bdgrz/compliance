using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed class SystemBoundary : Aggregate
{
    readonly Uuid _tenantId;
    bool _created;
    Uuid _programId;
    Uuid _initialDraftVersionId;
    BoundaryContent? _initialContent;
    Uuid _draftVersionId;
    long _draftRevision;
    BoundaryContent? _draftContent;
    Uuid _draftAuthorMemberId;
    bool _draftEverReviewed;
    Uuid _acceptedReviewDecisionId;
    Uuid _latestApprovedVersionId;
    readonly HashSet<Uuid> _approvedVersionIds = [];
    readonly Dictionary<ResponsibilityScope, ResponsibilitySet> _responsibilitySets = [];
    readonly Dictionary<ResponsibilityScope, List<ResponsibilityDecisionFact>> _responsibilityDecisions = [];
    DateOnly? _latestApprovedEffectiveFrom;
    long _revision;

    public bool IsCreated => _created;
    public bool IsVisible => _draftContent is not null || _latestApprovedVersionId != Uuid.Empty;
    public Uuid ProgramId => _programId;
    public long Revision => _revision;
    public Uuid DraftVersionId => _draftVersionId;
    public long DraftRevision => _draftRevision;
    public Uuid DraftAuthorMemberId => _draftAuthorMemberId;
    public Uuid LatestApprovedVersionId => _latestApprovedVersionId;
    public bool IsVersionApproved(Uuid versionId) => _approvedVersionIds.Contains(versionId);

    /// <summary>The initial approved version; any later approval changes the boundary.</summary>
    public Uuid FirstApprovedVersionId { get; private set; }

    public SystemBoundary(Uuid tenantId, Uuid boundaryId)
        : base(boundaryId, new EventStreamAddress(tenantId.ToString(), "boundaries", boundaryId.ToString()))
    {
        _tenantId = tenantId;
        On<BoundaryDraftCreated>(ev =>
        {
            _revision++;
            _created = true;
            _programId = ev.ProgramId;
            _initialDraftVersionId = ev.DraftVersionId;
            _initialContent = ev.Content;
            _draftVersionId = ev.DraftVersionId;
            _draftRevision = 1;
            _draftContent = ev.Content;
            _draftAuthorMemberId = ev.AuthorMemberId;
            _draftEverReviewed = false;
        });
        On<BoundaryDraftRevised>(ev =>
        {
            _revision++;
            _draftRevision = ev.Revision;
            _draftContent = ev.Content;
            _draftAuthorMemberId = ev.AuthorMemberId;
            _acceptedReviewDecisionId = Uuid.Empty;
        });
        On<BoundaryReviewed>(ev =>
        {
            _revision++;
            _acceptedReviewDecisionId = ev.Outcome == "accept" ? ev.DecisionId : Uuid.Empty;
            _draftEverReviewed = true;
            RecordResponsibilityDecision(new ResponsibilityScope("boundary", Id,
                    ev.DraftVersionId, ev.Revision), ev.ActorMemberId,
                ResponsibilityType.AssignedReviewer, ev.DecidedAt,
                ev.SeparationOfDutiesWaiverId);
        });
        On<BoundaryDraftDiscarded>(_ =>
        {
            _revision++;
            _draftVersionId = Uuid.Empty;
            _draftContent = null;
            _acceptedReviewDecisionId = Uuid.Empty;
        });
        On<BoundaryApproved>(ev =>
        {
            _revision++;
            RecordResponsibilityDecision(new ResponsibilityScope("boundary", Id,
                    ev.DraftVersionId, ev.Revision), ev.ActorMemberId,
                ResponsibilityType.PolicyApprover, ev.DecidedAt,
                ev.SeparationOfDutiesWaiverId);
            if (_approvedVersionIds.Count == 0)
                FirstApprovedVersionId = ev.DraftVersionId;
            _approvedVersionIds.Add(ev.DraftVersionId);
            _latestApprovedVersionId = ev.DraftVersionId;
            _latestApprovedEffectiveFrom = ev.EffectiveFrom;
            _draftVersionId = Uuid.Empty;
            _draftContent = null;
            _acceptedReviewDecisionId = Uuid.Empty;
        });
        On<BoundarySuccessorProposed>(ev =>
        {
            _revision++;
            _draftVersionId = ev.DraftVersionId;
            _draftRevision = 1;
            _draftContent = ev.Content;
            _draftAuthorMemberId = ev.AuthorMemberId;
            _draftEverReviewed = false;
            _acceptedReviewDecisionId = Uuid.Empty;
        });
        On<ResponsibilityAssigned>(ev => GetResponsibilitySet(ev.Scope).Apply(ev));
        On<ResponsibilityRevoked>(ev => GetResponsibilitySet(ev.Scope).Apply(ev));
    }

    public ResponsibilitySet GetResponsibilitySet(ResponsibilityScope scope)
    {
        if (!StringComparer.Ordinal.Equals(scope.RecordType, "boundary") || scope.RecordId != Id)
            throw new ArgumentException("The responsibility scope must belong to this boundary.", nameof(scope));
        if (!_responsibilitySets.TryGetValue(scope, out var set))
        {
            set = new ResponsibilitySet(_tenantId, scope);
            _responsibilitySets.Add(scope, set);
        }
        return set;
    }

    public CommandFailure? AssignResponsibility(ResponsibilityScope scope, Uuid assignmentId,
        Uuid memberId, ResponsibilityType type, Uuid assignedByMemberId,
        string assignedByDisplay, DateTimeOffset assignedAt, DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveUntil, IReadOnlyList<SeparationOfDutiesWaiver> waivers)
    {
        if (!IsCurrentResponsibilityScope(scope))
            return CommandFailure.StateConflict(
                "Responsibilities must target the current exact boundary draft revision.");
        var set = GetResponsibilitySet(scope);
        var proposed = new ResponsibilityAssignmentView(_tenantId, assignmentId, memberId,
            type, scope, assignedAt, assignedByMemberId, effectiveFrom, effectiveUntil,
            null, Uuid.Empty, []);
        var historicalConflict = HasUnwaivedHistoricalConflict(scope,
            set.ReadAssignments().Append(proposed));
        if (historicalConflict)
            return CommandFailure.StateConflict(
                "The responsibility would create an unwaived conflict with a prior decision on this exact revision.");
        return set.Assign(assignmentId, memberId, type,
            assignedByMemberId, assignedByDisplay, assignedAt, effectiveFrom, effectiveUntil,
            waivers, RaiseEvent);
    }

    public CommandFailure? RevokeResponsibility(ResponsibilityScope scope, Uuid assignmentId,
        Uuid memberId, string memberDisplay, DateTimeOffset revokedAt, string reason)
    {
        if (!IsCurrentResponsibilityScope(scope))
            return CommandFailure.StateConflict(
                "Responsibilities must target the current exact boundary draft revision.");
        return GetResponsibilitySet(scope).Revoke(assignmentId, memberId, memberDisplay,
            revokedAt, reason, RaiseEvent);
    }

    bool IsCurrentResponsibilityScope(ResponsibilityScope scope) =>
        StringComparer.Ordinal.Equals(scope.RecordType, "boundary") && scope.RecordId == Id &&
        _created && _draftContent is not null && scope.VersionId == _draftVersionId &&
        scope.Revision == _draftRevision;

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
        foreach (var decision in decisions)
        {
            if (decision.WaiverId is not null)
                continue;
            var decidedAction = new ResponsibilityAssignmentView(_tenantId, Uuid.Empty,
                decision.MemberId, decision.Type, scope, decision.At, Uuid.Empty,
                decision.At, decision.At.AddTicks(1), null, Uuid.Empty, []);
            if (ResponsibilityConflictPolicy.FindConflicts(assignments, decidedAction).Count > 0)
                return true;
        }
        return false;
    }

    sealed record ResponsibilityDecisionFact(Uuid MemberId, ResponsibilityType Type,
        DateTimeOffset At, Uuid? WaiverId);

    public Result<BoundaryRegistration> Create(Uuid programId, Uuid draftVersionId,
        BoundaryContent content, Uuid authorMemberId, string authorDisplay,
        DateTimeOffset changedAt)
    {
        if (_created)
            return programId == _programId && draftVersionId == _initialDraftVersionId &&
                   Equivalent(content, _initialContent)
                ? Result<BoundaryRegistration>.Success(new BoundaryRegistration(Id, _initialDraftVersionId))
                : Result<BoundaryRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The boundary already exists with different content."));
        if (programId == Uuid.Empty || draftVersionId == Uuid.Empty || authorMemberId == Uuid.Empty)
            return Result<BoundaryRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "The boundary requires a program, draft version, and author."));
        var validation = Validate(content);
        if (validation is not null)
            return Result<BoundaryRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation, validation));
        RaiseEvent(new BoundaryDraftCreated(_tenantId, Id, programId, draftVersionId,
            content, authorMemberId, authorDisplay, changedAt));
        return Result<BoundaryRegistration>.Success(new BoundaryRegistration(Id, draftVersionId));
    }

    public CommandFailure? Revise(Uuid draftVersionId, long expectedRevision,
        BoundaryContent content, Uuid authorMemberId, string authorDisplay,
        DateTimeOffset changedAt)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The boundary was not found.");
        if (_draftVersionId == Uuid.Empty)
            return CommandFailure.StateConflict(
                "The approved boundary is immutable. Propose a successor draft.");
        if (DraftVersionConflict(draftVersionId, expectedRevision) is { } conflict)
            return CommandFailure.ForVersion(conflict);
        var validation = Validate(content);
        if (validation is not null)
            return CommandFailure.InvalidContent(validation);
        RaiseEvent(new BoundaryDraftRevised(_tenantId, Id, draftVersionId,
            _draftRevision + 1, content, authorMemberId, authorDisplay, changedAt));
        return null;
    }

    VersionConflict? DraftVersionConflict(Uuid draftVersionId, long expectedRevision) =>
        _created && _draftVersionId != Uuid.Empty &&
        (draftVersionId != _draftVersionId || expectedRevision != _draftRevision)
            ? VersionedRecordRules.StaleDraft("boundary", _draftVersionId.ToGuid(), _draftRevision)
            : null;

    public CommandFailure? Review(Uuid draftVersionId, long expectedRevision, Uuid decisionId,
        string outcome, string rationale, Uuid reviewerMemberId, string reviewerDisplay,
        DateTimeOffset decidedAt, SeparationOfDutiesWaiver? separationOfDutiesWaiver = null)
    {
        var current = CheckDraft(draftVersionId, expectedRevision);
        if (current is not null)
            return current;
        var responsibilityScope = new ResponsibilityScope("boundary", Id, draftVersionId,
            expectedRevision);
        var responsibilityFailure = ResponsibilityDecisionGuard.Validate(
            GetResponsibilitySet(responsibilityScope), responsibilityScope, reviewerMemberId,
            ResponsibilityType.AssignedReviewer, decidedAt,
            reviewerMemberId == _draftAuthorMemberId, separationOfDutiesWaiver);
        if (responsibilityFailure is not null)
            return responsibilityFailure;
        var reviewWaiverScope = new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.Boundary, Id, draftVersionId,
            expectedRevision, SeparationOfDutiesActions.Review);
        if (separationOfDutiesWaiver is not null &&
            (separationOfDutiesWaiver.TenantId != _tenantId ||
             !separationOfDutiesWaiver.Allows(reviewWaiverScope, reviewerMemberId, decidedAt)))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this reviewer and draft revision.");
        if (reviewerMemberId == _draftAuthorMemberId && separationOfDutiesWaiver is null)
            return CommandFailure.ActorProhibited(
                "A boundary author cannot review their own draft.");
        if (outcome is not ("accept" or "request_changes") || string.IsNullOrWhiteSpace(rationale))
            return CommandFailure.InvalidContent("A review requires an outcome and rationale.");
        BoundaryReviewed reviewed = new(_tenantId, Id, draftVersionId, expectedRevision,
            decisionId, outcome, reviewerMemberId, reviewerDisplay, rationale.Trim(), decidedAt,
            separationOfDutiesWaiver?.Id)
        {
            StoredActor = ActorReference.ForMember(reviewerMemberId, reviewerDisplay),
        };
        RaiseEvent(reviewed);
        return null;
    }

    public CommandFailure? DiscardDraft(Uuid draftVersionId, long expectedRevision,
        string rationale, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset discardedAt)
    {
        var current = CheckDraft(draftVersionId, expectedRevision);
        if (current is not null)
            return current;
        if (VersionedRecordRules.DraftDiscardConflict(_draftEverReviewed) is { } referenced)
            return CommandFailure.ForVersion(referenced);
        if (string.IsNullOrWhiteSpace(rationale))
            return CommandFailure.InvalidContent("Discarding a draft requires a rationale.");
        RaiseEvent(new BoundaryDraftDiscarded(_tenantId, Id, draftVersionId,
            expectedRevision, actorMemberId, actorDisplay, rationale.Trim(), discardedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return null;
    }

    public CommandFailure? Approve(Uuid draftVersionId, long expectedRevision,
        Uuid approvalDecisionId, Uuid acceptedReviewDecisionId, DateOnly effectiveFrom, string rationale,
        string impactDigest, Uuid approverMemberId, string approverDisplay, DateTimeOffset decidedAt,
        SeparationOfDutiesWaiver? separationOfDutiesWaiver = null)
    {
        var current = CheckDraft(draftVersionId, expectedRevision);
        if (current is not null)
            return current;
        var responsibilityScope = new ResponsibilityScope("boundary", Id, draftVersionId,
            expectedRevision);
        var responsibilityFailure = ResponsibilityDecisionGuard.Validate(
            GetResponsibilitySet(responsibilityScope), responsibilityScope, approverMemberId,
            ResponsibilityType.PolicyApprover, decidedAt,
            approverMemberId == _draftAuthorMemberId, separationOfDutiesWaiver);
        if (responsibilityFailure is not null)
            return responsibilityFailure;
        var approvalWaiverScope = new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.Boundary, Id, draftVersionId,
            expectedRevision, SeparationOfDutiesActions.Approve);
        if (separationOfDutiesWaiver is not null &&
            (separationOfDutiesWaiver.TenantId != _tenantId ||
             !separationOfDutiesWaiver.Allows(approvalWaiverScope, approverMemberId, decidedAt)))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this approver and draft revision.");
        if (approverMemberId == _draftAuthorMemberId && separationOfDutiesWaiver is null)
            return CommandFailure.ActorProhibited(
                "A boundary author cannot approve their own draft.");
        // Drafts recorded before a content rule existed must satisfy it before approval.
        if (Validate(_draftContent) is { } invalidContent)
            return CommandFailure.InvalidContent(invalidContent);
        if (acceptedReviewDecisionId == Uuid.Empty ||
            acceptedReviewDecisionId != _acceptedReviewDecisionId)
            return CommandFailure.StateConflict(
                "Approval requires the latest accepted review of this draft revision.");
        if (string.IsNullOrWhiteSpace(rationale))
            return CommandFailure.InvalidContent("Approval requires a rationale.");
        if (string.IsNullOrWhiteSpace(impactDigest))
            return CommandFailure.InvalidContent(
                "Approval requires the acknowledged impact preview digest.");
        if (_latestApprovedEffectiveFrom is { } prior &&
            !EffectiveInterval.CanFollow(prior, effectiveFrom))
            return CommandFailure.InvalidContent(
                "A successor must become effective after the previous approved version.");
        BoundaryApproved approved = new(_tenantId, Id, draftVersionId, expectedRevision,
            approvalDecisionId, acceptedReviewDecisionId, approverMemberId, approverDisplay,
            rationale.Trim(), effectiveFrom, decidedAt, impactDigest,
            separationOfDutiesWaiver?.Id)
        {
            StoredActor = ActorReference.ForMember(approverMemberId, approverDisplay),
        };
        RaiseEvent(approved);
        return null;
    }

    public CommandFailure? ProposeSuccessor(Uuid expectedApprovedVersionId,
        Uuid draftVersionId, BoundaryContent content, Uuid authorMemberId,
        string authorDisplay, DateTimeOffset changedAt)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The boundary was not found.");
        if (_latestApprovedVersionId == Uuid.Empty)
            return CommandFailure.StateConflict("The boundary has no approved version.");
        if (expectedApprovedVersionId != _latestApprovedVersionId)
            return CommandFailure.ForVersion(
                VersionedRecordRules.StaleApprovedVersion("boundary",
                    _latestApprovedVersionId.ToGuid()));
        if (_draftVersionId != Uuid.Empty)
            return CommandFailure.StateConflict("The boundary already has an open successor draft.");
        var validation = Validate(content);
        if (validation is not null)
            return CommandFailure.InvalidContent(validation);
        RaiseEvent(new BoundarySuccessorProposed(_tenantId, Id, draftVersionId,
            _latestApprovedVersionId, content, authorMemberId, authorDisplay, changedAt));
        return null;
    }

    CommandFailure? CheckDraft(Uuid draftVersionId, long expectedRevision)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The boundary was not found.");
        if (_draftVersionId == Uuid.Empty)
            return CommandFailure.StateConflict(
                "The approved boundary is immutable. Propose a successor draft.");
        return DraftVersionConflict(draftVersionId, expectedRevision) is { } conflict
            ? CommandFailure.ForVersion(conflict)
            : null;
    }

    static string? Validate(BoundaryContent? content)
    {
        if (content is null || string.IsNullOrWhiteSpace(content.Statement))
            return "A boundary requires a statement.";
        if (content.EngagementStage is not ("readiness" or "type_i" or "type_ii"))
            return "The engagement stage must be readiness, type_i, or type_ii.";
        if (content.TrustServicesCategories is null || content.TrustServicesCategories.Count == 0 ||
            content.TrustServicesCategories.Any(category =>
                category is not ("security" or "availability" or "processing_integrity" or
                    "confidentiality" or "privacy")) ||
            content.TrustServicesCategories.Distinct(StringComparer.Ordinal).Count() !=
            content.TrustServicesCategories.Count)
            return "The boundary requires distinct, recognized Trust Services categories.";
        if (!content.TrustServicesCategories.Contains("security", StringComparer.Ordinal))
            return "The boundary requires the security category; optional categories add to it.";
        if (content.Entries is null || content.Entries.Any(entry =>
                entry is null || entry.EntryId == Uuid.Empty ||
                entry.Kind is not ("inclusion" or "exclusion" or "assumption" or "question") ||
                entry.SubjectType is not ("service" or "person" or "application" or
                    "system_instance" or
                    "component" or "information" or "data_flow" or "process" or
                    "location" or "provider" or "commitment") ||
                string.IsNullOrWhiteSpace(entry.Subject) ||
                string.IsNullOrWhiteSpace(entry.OwnerReference) ||
                string.IsNullOrWhiteSpace(entry.Rationale) ||
                entry.GovernedRecordId == Uuid.Empty ||
                (entry.Unresolved && entry.GovernedRecordId is not null) ||
                (!entry.Unresolved && entry.GovernedRecordId is null)) ||
            content.Entries.Select(static entry => entry.EntryId).Distinct().Count() !=
            content.Entries.Count)
            return "Every scope entry requires a unique ID, typed subject, owner, rationale, and a consistent governed reference state.";
        return null;
    }

    static bool Equivalent(BoundaryContent? left, BoundaryContent? right) =>
        left is not null && right is not null &&
        left.Statement == right.Statement && left.EngagementStage == right.EngagementStage &&
        left.TrustServicesCategories.SequenceEqual(right.TrustServicesCategories) &&
        left.Entries.SequenceEqual(right.Entries);
}
