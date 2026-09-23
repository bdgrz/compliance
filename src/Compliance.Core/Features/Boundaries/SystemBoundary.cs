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
    DateOnly? _latestApprovedEffectiveFrom;
    long _revision;

    public bool IsCreated => _created;
    public bool IsVisible => _draftContent is not null || _latestApprovedVersionId != Uuid.Empty;
    public Uuid ProgramId => _programId;
    public long Revision => _revision;
    public Uuid LatestApprovedVersionId => _latestApprovedVersionId;
    public bool IsVersionApproved(Uuid versionId) => _approvedVersionIds.Contains(versionId);

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
    }

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
            ? VersionedRecordRules.StaleDraft("boundary", _draftVersionId, _draftRevision)
            : null;

    public CommandFailure? Review(Uuid draftVersionId, long expectedRevision, Uuid decisionId,
        string outcome, string rationale, Uuid reviewerMemberId, string reviewerDisplay,
        DateTimeOffset decidedAt)
    {
        var current = CheckDraft(draftVersionId, expectedRevision);
        if (current is not null)
            return current;
        if (reviewerMemberId == _draftAuthorMemberId)
            return CommandFailure.ActorProhibited(
                "A boundary author cannot review their own draft.");
        if (outcome is not ("accept" or "request_changes") || string.IsNullOrWhiteSpace(rationale))
            return CommandFailure.InvalidContent("A review requires an outcome and rationale.");
        RaiseEvent(new BoundaryReviewed(_tenantId, Id, draftVersionId, expectedRevision,
            decisionId, outcome, reviewerMemberId, reviewerDisplay, rationale.Trim(), decidedAt));
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
            expectedRevision, actorMemberId, actorDisplay, rationale.Trim(), discardedAt));
        return null;
    }

    public CommandFailure? Approve(Uuid draftVersionId, long expectedRevision,
        Uuid approvalDecisionId, Uuid acceptedReviewDecisionId, DateOnly effectiveFrom, string rationale,
        string impactDigest, Uuid approverMemberId, string approverDisplay, DateTimeOffset decidedAt)
    {
        var current = CheckDraft(draftVersionId, expectedRevision);
        if (current is not null)
            return current;
        if (approverMemberId == _draftAuthorMemberId)
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
        RaiseEvent(new BoundaryApproved(_tenantId, Id, draftVersionId, expectedRevision,
            approvalDecisionId, acceptedReviewDecisionId, approverMemberId, approverDisplay,
            rationale.Trim(), effectiveFrom, decidedAt, impactDigest));
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
                VersionedRecordRules.StaleApprovedVersion("boundary", _latestApprovedVersionId));
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
