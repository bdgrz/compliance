using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>
///     The Policy record: its draft line, independent review and approval decisions, immutable
///     approved versions with half-open effective intervals, periodic review, successor and
///     retirement proposals, and narrowly permitted deletion of an unused draft.
/// </summary>
public sealed class Policy : Aggregate
{
    // Fitz stream frames are u16; leave room for the Portia envelope and metadata.
    public const int MaximumEventPayloadBytes = 48 * 1024;
    public const int MaximumBodyLength = 20000;
    readonly Uuid _tenantId;
    bool _created;
    bool _discarded;
    bool _retired;
    bool _everReviewed;
    long _revision;
    string? _identifier;
    Uuid _createRequestId;
    PolicyContent? _initialContent;
    PolicyContent? _draft;
    string? _draftSha;
    bool _draftOpen;
    long? _draftPredecessor;
    AcceptedReview? _acceptedReview;
    string? _latestReviewOutcome;
    PendingRetirement? _pendingRetirement;
    DateOnly? _lastReviewedOn;
    ActorReference? _lastChangedBy;
    DateTimeOffset _lastChangedAt;
    readonly HashSet<Uuid> _pendingAuthors = [];
    readonly List<PolicyVersionView> _versions = [];
    readonly List<PolicyDecisionView> _decisions = [];

    public bool IsVisible => _created && !_discarded;
    public long Revision => _revision;
    public Uuid ProgramId { get; private set; }
    public string? Identifier => _identifier;
    public PolicyContent? DraftContent => _draftOpen ? _draft : null;
    public long? DraftPredecessorVersion => _draftOpen ? _draftPredecessor : null;
    public IReadOnlySet<Uuid> PendingAuthorMemberIds => new HashSet<Uuid>(_pendingAuthors);
    public Uuid? AcceptedReviewerMemberId => _acceptedReview?.ReviewerMemberId;
    public bool IsRetired => _retired;
    public PolicyVersionView? CurrentVersion => _versions.LastOrDefault();
    public IReadOnlyList<PolicyVersionView> ReadVersions() => _versions.ToArray();
    public IReadOnlyList<PolicyDecisionView> ReadDecisions() => _decisions.ToArray();

    public Policy(Uuid tenantId, Uuid policyId)
        : base(policyId, new EventStreamAddress(tenantId.ToString(), "policies",
            policyId.ToString()))
    {
        _tenantId = tenantId;
        On<PolicyDraftCreated>(ev =>
        {
            _created = true;
            _revision = 1;
            ProgramId = ev.ProgramId;
            _identifier = ev.Identifier;
            _createRequestId = ev.CreateRequestId;
            _initialContent = ev.Content;
            _draft = ev.Content;
            _draftSha = ev.ContentSha256;
            _draftOpen = true;
            _pendingAuthors.Add(ev.ActorMemberId);
            Touch(ev.Actor, ev.ChangedAt);
        });
        On<PolicyDraftRevised>(ev =>
        {
            if (!_draftOpen)
            {
                _pendingAuthors.Clear();
                _draftPredecessor = ev.PredecessorVersion;
            }
            _revision = ev.Revision;
            _draft = ev.Content;
            _draftSha = ev.ContentSha256;
            _draftOpen = true;
            _acceptedReview = null;
            _latestReviewOutcome = null;
            _pendingAuthors.Add(ev.ActorMemberId);
            Touch(ev.Actor, ev.ChangedAt);
        });
        On<PolicyReviewed>(ev =>
        {
            _everReviewed = true;
            _latestReviewOutcome = ev.Outcome;
            _acceptedReview = ev.Outcome == "accept"
                ? new AcceptedReview(ev.DecisionId, ev.ActorMemberId)
                : null;
            if (ev.Outcome != "accept" && !_draftOpen)
                _pendingRetirement = null;
            _decisions.Add(new PolicyDecisionView(ev.TenantId, ev.ProgramId, ev.PolicyId,
                ev.DecisionId, "review", ev.Outcome, ev.Revision, null, ev.Rationale, ev.Actor,
                ev.DecidedAt, ev.SeparationOfDutiesWaiverId));
            Touch(ev.Actor, ev.DecidedAt);
        });
        On<PolicyApproved>(ev =>
        {
            if (ev.PredecessorVersion is { } predecessor)
            {
                var index = _versions.FindIndex(version => version.Version == predecessor);
                if (index >= 0)
                    _versions[index] = _versions[index] with
                    {
                        Status = "superseded",
                        EffectiveUntil = ev.EffectiveFrom,
                    };
            }
            _versions.Add(new PolicyVersionView(ev.TenantId, ev.ProgramId, ev.PolicyId,
                _identifier!, ev.Version, ev.Revision, ev.Major, "approved", _draft!,
                ev.ContentSha256, ev.EffectiveFrom, null, ev.PredecessorVersion, ev.DecisionId,
                ev.AcceptedReviewDecisionId, ev.Actor, ev.DecidedAt, ev.ImpactDigest,
                ev.SeparationOfDutiesWaiverId));
            _decisions.Add(new PolicyDecisionView(ev.TenantId, ev.ProgramId, ev.PolicyId,
                ev.DecisionId, "approval", "approve", ev.Revision, ev.Version, ev.Rationale,
                ev.Actor, ev.DecidedAt, ev.SeparationOfDutiesWaiverId));
            _lastReviewedOn = DateOnly.FromDateTime(ev.DecidedAt.UtcDateTime);
            _draftOpen = false;
            _draftPredecessor = null;
            _acceptedReview = null;
            _latestReviewOutcome = null;
            _pendingAuthors.Clear();
            Touch(ev.Actor, ev.DecidedAt);
        });
        On<PolicyPeriodicReviewConfirmed>(ev =>
        {
            _lastReviewedOn = ev.ReviewedOn;
            _decisions.Add(new PolicyDecisionView(ev.TenantId, ev.ProgramId, ev.PolicyId,
                ev.DecisionId, "periodic_review", "confirmed", null, ev.Version, ev.Rationale,
                ev.Actor, ev.DecidedAt, null));
            Touch(ev.Actor, ev.DecidedAt);
        });
        On<PolicyRetirementProposed>(ev =>
        {
            _revision = ev.Revision;
            _pendingAuthors.Clear();
            _pendingAuthors.Add(ev.ActorMemberId);
            _acceptedReview = null;
            _latestReviewOutcome = null;
            _pendingRetirement = new PendingRetirement(ev.Version, ev.EffectiveUntil,
                ev.Rationale, ev.Actor, ev.ProposedAt);
            Touch(ev.Actor, ev.ProposedAt);
        });
        On<PolicyRetired>(ev =>
        {
            var index = _versions.FindIndex(version => version.Version == ev.Version);
            if (index >= 0)
                _versions[index] = _versions[index] with
                {
                    Status = "retired",
                    EffectiveUntil = ev.EffectiveUntil,
                };
            _decisions.Add(new PolicyDecisionView(ev.TenantId, ev.ProgramId, ev.PolicyId,
                ev.DecisionId, "retirement", "retire", ev.Revision, ev.Version, ev.Rationale,
                ev.Actor, ev.DecidedAt, ev.SeparationOfDutiesWaiverId));
            _retired = true;
            _pendingRetirement = null;
            _acceptedReview = null;
            _pendingAuthors.Clear();
            Touch(ev.Actor, ev.DecidedAt);
        });
        On<PolicyDraftDiscarded>(ev =>
        {
            _discarded = true;
            _draftOpen = false;
            Touch(ev.Actor, ev.DiscardedAt);
        });
    }

    sealed record AcceptedReview(Uuid DecisionId, Uuid ReviewerMemberId);

    sealed record PendingRetirement(long Version, DateOnly EffectiveUntil, string Rationale,
        ActorReference ProposedBy, DateTimeOffset ProposedAt);

    void Touch(ActorReference actor, DateTimeOffset at)
    {
        _lastChangedBy = actor;
        _lastChangedAt = at;
    }

    public static string NormalizeIdentifier(string? identifier) =>
        identifier?.Trim().ToUpperInvariant() ?? string.Empty;

    public static Uuid IdFor(Uuid tenantId, Uuid programId, string identifier) =>
        Uuid.CreateVersion5(Uuid.CreateVersion5(tenantId, programId.ToString()),
            "policy:" + NormalizeIdentifier(identifier));

    /// <summary>The approved version whose half-open effective interval contains the date.</summary>
    public PolicyVersionView? EffectiveVersion(DateOnly date) => _versions.LastOrDefault(
        version => new EffectiveInterval(version.EffectiveFrom, version.EffectiveUntil)
            .Contains(date));

    public PolicyVersionView? FindVersion(long version) =>
        _versions.FirstOrDefault(candidate => candidate.Version == version);

    public PolicyDecisionView? FindDecision(Uuid decisionId) =>
        _decisions.LastOrDefault(decision => decision.DecisionId == decisionId);

    /// <summary>The next periodic review date of the current version.</summary>
    public DateOnly? NextReviewDueOn => _retired || CurrentVersion is not { } current ||
                                        _lastReviewedOn is not { } reviewed
        ? null
        : reviewed.AddMonths(current.Content.ReviewCadenceMonths);

    public PolicyView ToView(DateOnly today)
    {
        if (!IsVisible)
            throw new InvalidOperationException("A missing policy has no view.");
        var current = CurrentVersion;
        var next = NextReviewDueOn;
        return new PolicyView(_tenantId, ProgramId, Id, _identifier!, Status, PendingStatus,
            _revision, DraftContent, _draftOpen ? _draftSha : null, DraftPredecessorVersion,
            _acceptedReview?.DecisionId, current?.Version, current?.EffectiveFrom,
            _lastReviewedOn, next, next is { } due && today > due,
            _pendingRetirement is { } retirement
                ? new PolicyRetirementProposalView(retirement.Version, retirement.EffectiveUntil,
                    retirement.Rationale, retirement.ProposedBy, retirement.ProposedAt)
                : null,
            _lastChangedBy!, _lastChangedAt);
    }

    public string Status => _retired ? "retired" : _versions.Count > 0 ? "approved" : "draft";

    public string? PendingStatus
    {
        get
        {
            if (_retired)
                return null;
            if (_pendingRetirement is not null)
                return _acceptedReview is null ? "retirement_proposed" : "retirement_awaiting_approval";
            if (!_draftOpen)
                return null;
            if (_acceptedReview is not null)
                return "awaiting_approval";
            return _latestReviewOutcome == "request_changes" ? "changes_requested" : "draft";
        }
    }

    public Result<PolicyRegistration> Create(Uuid programId, Uuid createRequestId,
        string identifier, PolicyContent content, ActorReference actor, Uuid actorMemberId,
        DateTimeOffset changedAt)
    {
        var normalized = NormalizeIdentifier(identifier);
        if ((ValidateIdentifier(normalized) ?? ValidateContent(content)) is { } error)
            return Result<PolicyRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation, error));
        var clean = Clean(content);
        if (_created)
        {
            if (_discarded)
                return Result<PolicyRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The discarded policy draft cannot be recreated."));
            return ProgramId == programId && _createRequestId == createRequestId &&
                   Hash(_initialContent!) == Hash(clean)
                ? Result<PolicyRegistration>.Success(new PolicyRegistration(Id, normalized, 1))
                : Result<PolicyRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The policy identifier already exists."));
        }
        var created = new PolicyDraftCreated(_tenantId, programId, Id, createRequestId,
            normalized, clean, Hash(clean), actor, actorMemberId, changedAt);
        if (Size(created, ComplianceCoreJsonContext.Default.PolicyDraftCreated) >
            MaximumEventPayloadBytes)
            return Result<PolicyRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "The policy draft exceeds the bounded event payload size."));
        RaiseEvent(created);
        return Result<PolicyRegistration>.Success(new PolicyRegistration(Id, normalized, 1));
    }

    public CommandFailure? Revise(Uuid programId, long expectedRevision, PolicyContent content,
        ActorReference actor, Uuid actorMemberId, DateTimeOffset changedAt)
    {
        if (!IsVisible || ProgramId != programId)
            return CommandFailure.MissingRecord("The policy was not found.");
        if (_retired)
            return CommandFailure.StateConflict("A retired policy cannot be revised.");
        if (!_draftOpen)
            return CommandFailure.StateConflict(
                "The approved policy version is immutable. Propose a successor draft.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("policy draft",
                _revision));
        return RaiseRevision(programId, content, null, actor, actorMemberId, changedAt);
    }

    /// <summary>
    ///     Opens a successor draft from the exact current approved version. The predecessor stays
    ///     effective and immutable until the successor is reviewed and approved.
    /// </summary>
    public CommandFailure? ProposeSuccessor(Uuid programId, long expectedVersion,
        PolicyContent content, ActorReference actor, Uuid actorMemberId,
        DateTimeOffset changedAt)
    {
        if (!IsVisible || ProgramId != programId)
            return CommandFailure.MissingRecord("The policy was not found.");
        if (_retired)
            return CommandFailure.StateConflict("A retired policy cannot have a successor.");
        if (CurrentVersion is not { } current)
            return CommandFailure.StateConflict(
                "The policy has no approved version. Revise its initial draft instead.");
        if (expectedVersion != current.Version)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("policy version",
                current.Version));
        if (_draftOpen)
            return CommandFailure.StateConflict("The policy already has an open successor draft.");
        if (_pendingRetirement is not null)
            return CommandFailure.StateConflict("The policy has a pending retirement.");
        return RaiseRevision(programId, content, current.Version, actor, actorMemberId,
            changedAt);
    }

    CommandFailure? RaiseRevision(Uuid programId, PolicyContent content, long? predecessor,
        ActorReference actor, Uuid actorMemberId, DateTimeOffset changedAt)
    {
        if (ValidateContent(content) is { } error)
            return CommandFailure.InvalidContent(error);
        var clean = Clean(content);
        var revised = new PolicyDraftRevised(_tenantId, programId, Id, _revision + 1, clean,
            Hash(clean), predecessor, actor, actorMemberId, changedAt);
        if (Size(revised, ComplianceCoreJsonContext.Default.PolicyDraftRevised) >
            MaximumEventPayloadBytes)
            return CommandFailure.InvalidContent(
                "The policy draft exceeds the bounded event payload size.");
        RaiseEvent(revised);
        return null;
    }

    /// <summary>Deletes only a never-approved, never-reviewed draft with no applicability.</summary>
    public CommandFailure? Discard(Uuid programId, long expectedRevision, string rationale,
        ActorReference actor, DateTimeOffset discardedAt)
    {
        if (!IsVisible || ProgramId != programId)
            return CommandFailure.MissingRecord("The policy draft was not found.");
        if (_versions.Count > 0)
            return CommandFailure.StateConflict(
                "An approved, superseded, or retired policy cannot be deleted. Supersede or retire it instead.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("policy draft",
                _revision));
        if (VersionedRecordRules.DraftDiscardConflict(_everReviewed) is { } referenced)
            return CommandFailure.ForVersion(referenced);
        if (_draft?.Applicability is { Count: > 0 })
            return CommandFailure.StateConflict(
                "Deleting a policy draft requires no retained applicability relationships.");
        if (string.IsNullOrWhiteSpace(rationale) || rationale.Length > 4000)
            return CommandFailure.InvalidContent("Deleting a policy draft requires a rationale.");
        RaiseEvent(new PolicyDraftDiscarded(_tenantId, programId, Id, _revision,
            rationale.Trim(), actor, discardedAt));
        return null;
    }

    public CommandFailure? Review(Uuid programId, long expectedRevision, Uuid decisionId,
        string outcome, string rationale, ActorReference actor, Uuid reviewerMemberId,
        DateTimeOffset decidedAt, SeparationOfDutiesWaiver? waiver = null)
    {
        if (CheckPendingTarget(programId, expectedRevision) is { } target)
            return target;
        if (CheckIndependence(reviewerMemberId, _pendingAuthors.Contains(reviewerMemberId),
                SeparationOfDutiesActions.Review, decidedAt, waiver,
                "A policy author cannot review a revision they proposed.") is { } sod)
            return sod;
        if (decisionId == Uuid.Empty || outcome is not ("accept" or "request_changes") ||
            !IsBoundedText(rationale))
            return CommandFailure.InvalidContent(
                "A policy review requires an outcome of accept or request_changes and a rationale of at most 4000 characters.");
        RaiseEvent(new PolicyReviewed(_tenantId, programId, Id, expectedRevision, decisionId,
            outcome, rationale.Trim(), actor, reviewerMemberId, decidedAt, waiver?.Id));
        return null;
    }

    /// <summary>
    ///     Approves the exact reviewed revision as the next immutable version. The approver must be
    ///     independent of every author of the revision and of its accepted reviewer.
    /// </summary>
    public CommandFailure? Approve(Uuid programId, long expectedRevision, Uuid decisionId,
        Uuid acceptedReviewDecisionId, DateOnly effectiveFrom, bool major, string rationale,
        string? impactDigest, ActorReference actor, Uuid approverMemberId,
        DateTimeOffset decidedAt, SeparationOfDutiesWaiver? waiver = null)
    {
        if (CheckPendingTarget(programId, expectedRevision) is { } target)
            return target;
        if (!_draftOpen)
            return CommandFailure.StateConflict(
                "Only an open policy draft can be approved. Retirement uses its own approval.");
        var conflict = _pendingAuthors.Contains(approverMemberId) ||
                       _acceptedReview?.ReviewerMemberId == approverMemberId;
        if (CheckIndependence(approverMemberId, conflict, SeparationOfDutiesActions.Approve,
                decidedAt, waiver,
                "A policy approver must be independent of its authors and accepted reviewer.") is
            { } sod)
            return sod;
        if (_acceptedReview is not { } accepted || accepted.DecisionId != acceptedReviewDecisionId)
            return CommandFailure.StateConflict(
                "Approval requires the latest accepted review of this exact draft revision.");
        if (decisionId == Uuid.Empty || !IsBoundedText(rationale) || effectiveFrom == default)
            return CommandFailure.InvalidContent(
                "Approval requires an effective start date and a rationale of at most 4000 characters.");
        if (string.IsNullOrWhiteSpace(_draft!.OwnerReference))
            return CommandFailure.InvalidContent("Approval requires an accountable policy owner.");
        var predecessor = _draftPredecessor is { } predecessorVersion
            ? FindVersion(predecessorVersion)
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
        RaiseEvent(new PolicyApproved(_tenantId, programId, Id, expectedRevision,
            _versions.Count + 1, predecessor is null || major, decisionId,
            acceptedReviewDecisionId, _draftSha!, effectiveFrom, predecessor?.Version,
            predecessor is null ? null : impactDigest!.Trim(), rationale.Trim(), actor,
            approverMemberId, decidedAt, waiver?.Id));
        return null;
    }

    /// <summary>Confirms the current version at its periodic review, restarting its cadence.</summary>
    public CommandFailure? ConfirmPeriodicReview(Uuid programId, long expectedVersion,
        Uuid decisionId, string rationale, ActorReference actor, Uuid reviewerMemberId,
        DateTimeOffset decidedAt)
    {
        if (!IsVisible || ProgramId != programId)
            return CommandFailure.MissingRecord("The policy was not found.");
        if (_retired)
            return CommandFailure.StateConflict("A retired policy has no periodic review.");
        if (CurrentVersion is not { } current)
            return CommandFailure.StateConflict("Only an approved policy has a periodic review.");
        if (expectedVersion != current.Version)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("policy version",
                current.Version));
        if (decisionId == Uuid.Empty || !IsBoundedText(rationale))
            return CommandFailure.InvalidContent(
                "A periodic review requires a rationale of at most 4000 characters.");
        RaiseEvent(new PolicyPeriodicReviewConfirmed(_tenantId, programId, Id, current.Version,
            decisionId, DateOnly.FromDateTime(decidedAt.UtcDateTime), rationale.Trim(), actor,
            reviewerMemberId, decidedAt));
        return null;
    }

    public CommandFailure? ProposeRetirement(Uuid programId, long expectedVersion,
        DateOnly effectiveUntil, string rationale, ActorReference actor, Uuid actorMemberId,
        DateTimeOffset proposedAt)
    {
        if (!IsVisible || ProgramId != programId)
            return CommandFailure.MissingRecord("The policy was not found.");
        if (_retired)
            return CommandFailure.StateConflict("The policy is already retired.");
        if (CurrentVersion is not { } current)
            return CommandFailure.StateConflict(
                "Only an approved policy can be retired. Delete an unused draft instead.");
        if (expectedVersion != current.Version)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("policy version",
                current.Version));
        if (_draftOpen)
            return CommandFailure.StateConflict(
                "The policy has an open successor draft. Approve it before proposing retirement.");
        if (_pendingRetirement is not null)
            return CommandFailure.StateConflict("The policy already has a pending retirement.");
        if (effectiveUntil == default || !EffectiveInterval.CanFollow(current.EffectiveFrom,
                effectiveUntil))
            return CommandFailure.InvalidContent(
                "Retirement must take effect after the current version's effective start.");
        if (!IsBoundedText(rationale))
            return CommandFailure.InvalidContent(
                "Retirement requires a rationale of at most 4000 characters.");
        RaiseEvent(new PolicyRetirementProposed(_tenantId, programId, Id, _revision + 1,
            current.Version, effectiveUntil, rationale.Trim(), actor, actorMemberId, proposedAt));
        return null;
    }

    public CommandFailure? ApproveRetirement(Uuid programId, long expectedRevision,
        Uuid decisionId, Uuid acceptedReviewDecisionId, string rationale, ActorReference actor,
        Uuid approverMemberId, DateTimeOffset decidedAt, SeparationOfDutiesWaiver? waiver = null)
    {
        if (CheckPendingTarget(programId, expectedRevision) is { } target)
            return target;
        if (_pendingRetirement is not { } pending)
            return CommandFailure.StateConflict("The policy has no pending retirement.");
        var conflict = _pendingAuthors.Contains(approverMemberId) ||
                       _acceptedReview?.ReviewerMemberId == approverMemberId;
        if (CheckIndependence(approverMemberId, conflict, SeparationOfDutiesActions.Approve,
                decidedAt, waiver,
                "A retirement approver must be independent of its proposer and accepted reviewer.") is
            { } sod)
            return sod;
        if (_acceptedReview is not { } accepted || accepted.DecisionId != acceptedReviewDecisionId)
            return CommandFailure.StateConflict(
                "Retirement requires the latest accepted review of this exact retirement proposal.");
        if (decisionId == Uuid.Empty || !IsBoundedText(rationale))
            return CommandFailure.InvalidContent(
                "Retirement approval requires a rationale of at most 4000 characters.");
        RaiseEvent(new PolicyRetired(_tenantId, programId, Id, expectedRevision, pending.Version,
            decisionId, acceptedReviewDecisionId, pending.EffectiveUntil, rationale.Trim(), actor,
            approverMemberId, decidedAt, waiver?.Id));
        return null;
    }

    CommandFailure? CheckPendingTarget(Uuid programId, long expectedRevision)
    {
        if (!IsVisible || ProgramId != programId)
            return CommandFailure.MissingRecord("The policy was not found.");
        if (_retired)
            return CommandFailure.StateConflict("The policy is retired.");
        if (!_draftOpen && _pendingRetirement is null)
            return CommandFailure.StateConflict(
                "The policy has no pending draft or retirement to decide.");
        return expectedRevision != _revision
            ? CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("policy", _revision))
            : null;
    }

    CommandFailure? CheckIndependence(Uuid memberId, bool conflict, string action,
        DateTimeOffset at, SeparationOfDutiesWaiver? waiver, string message)
    {
        if (waiver is null)
            return conflict ? CommandFailure.ActorProhibited(message) : null;
        var scope = new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.Policy, Id, Id,
            _revision, action);
        if (waiver.TenantId != _tenantId || !waiver.Allows(scope, memberId, at))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this member and policy revision.");
        return conflict
            ? null
            : CommandFailure.ActorProhibited(
                "A separation-of-duties waiver may only be used for a current conflict.");
    }

    static bool IsBoundedText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 4000;

    static readonly HashSet<string> SubjectTypes = new(StringComparer.Ordinal)
    {
        "application", "system_instance", "control", "criterion", "risk", "vendor", "process",
        "scope",
    };

    static string? ValidateIdentifier(string identifier) =>
        identifier.Length is < 1 or > 80 ||
        !identifier.All(static c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) ||
                                    c is '-' or '_' or '.')
            ? "A policy identifier requires 1 to 80 ASCII letters, digits, hyphens, underscores, or periods."
            : null;

    /// <summary>Returns the first content rule the policy content breaks, or null.</summary>
    public static string? ValidateContent(PolicyContent? content)
    {
        if (content is null || string.IsNullOrWhiteSpace(content.Title) ||
            content.Title.Trim().Length > 200 || string.IsNullOrWhiteSpace(content.Purpose) ||
            content.Purpose.Length > 4000)
            return "A policy requires a title of at most 200 characters and a purpose of at most 4000 characters.";
        if (PolicyAudience.Validate(content.AudienceKind, content.AudienceTeams) is { } audience)
            return audience;
        if (content.ReviewCadenceMonths is < 1 or > 36)
            return "The review cadence must be between 1 and 36 months.";
        if (string.IsNullOrWhiteSpace(content.Body) &&
            string.IsNullOrWhiteSpace(content.SourceReference))
            return "A policy requires authored content or a source document reference.";
        if (content.Body?.Length > MaximumBodyLength || content.SourceReference?.Length > 1024 ||
            content.OwnerReference?.Length > 200)
            return $"The policy body must be at most {MaximumBodyLength} characters, its source reference at most 1024, and its owner at most 200.";
        var applicability = content.Applicability ?? [];
        if (applicability.Count > 100 || applicability.Any(static reference =>
                reference is null || !SubjectTypes.Contains(reference.SubjectType) ||
                string.IsNullOrWhiteSpace(reference.Subject) || reference.Subject.Length > 500 ||
                reference.RecordId == Uuid.Empty))
            return "Every applicability reference requires a supported subject type and a subject of at most 500 characters.";
        return applicability.Select(Key).Distinct(StringComparer.Ordinal).Count() !=
               applicability.Count
            ? "Applicability references must be unique."
            : null;
    }

    /// <summary>The identity of an applicability relationship across versions.</summary>
    public static string Key(PolicyApplicabilityReference reference) =>
        reference.SubjectType + ":" + (reference.RecordId?.ToString() ??
                                       reference.Subject.Trim().ToUpperInvariant());

    static PolicyContent Clean(PolicyContent content) => new(content.Title.Trim(),
        content.Purpose.Trim(), content.AudienceKind,
        PolicyAudience.CleanTeams(content.AudienceTeams),
        content.ReviewCadenceMonths, string.IsNullOrWhiteSpace(content.Body) ? null : content.Body,
        string.IsNullOrWhiteSpace(content.SourceReference) ? null : content.SourceReference.Trim(),
        string.IsNullOrWhiteSpace(content.OwnerReference) ? null : content.OwnerReference.Trim(),
        (content.Applicability ?? []).Select(static reference => new PolicyApplicabilityReference(
            reference.SubjectType, reference.Subject.Trim(), reference.RecordId)).ToArray());

    /// <summary>The SHA-256 of the canonical content, used as the acknowledged policy hash.</summary>
    public static string Hash(PolicyContent content) => Convert.ToHexStringLower(
        System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(content,
            ComplianceCoreJsonContext.Default.PolicyContent)));

    static int Size<T>(T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> info) =>
        JsonSerializer.SerializeToUtf8Bytes(value, info).Length;
}
