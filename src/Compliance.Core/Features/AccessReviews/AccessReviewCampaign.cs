using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;
using System.Globalization;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     One access-review campaign. Launch freezes its items, reviewers, instructions, and deadline;
///     decisions, provider changes, verifications, and exceptions are appended; completion requires
///     every item decided and every required remediation verified or excepted (M0-D07).
/// </summary>
public sealed class AccessReviewCampaign : Aggregate
{
    public const string Area = "access-review-campaigns";
    public const string Active = "active";
    public const string Completed = "completed";

    readonly Uuid _tenantId;
    readonly Dictionary<Uuid, ItemState> _items = [];
    readonly List<Uuid> _order = [];

    public AccessReviewCampaign(Uuid tenantId, Uuid campaignId)
        : base(campaignId, new EventStreamAddress(tenantId.ToString(), Area, campaignId.ToString()))
    {
        _tenantId = tenantId;
        On<AccessReviewCampaignLaunched>(ev =>
        {
            Launched = ev;
            Revision = 1;
            foreach (var item in ev.Items)
            {
                _items[item.ItemId] = new ItemState(item);
                _order.Add(item.ItemId);
            }
        });
        On<AccessDecisionsRecorded>(ev =>
        {
            Revision = ev.Revision;
            foreach (var decision in ev.Decisions)
                _items[decision.ItemId].Decisions.Add(decision);
        });
        On<AccessRemediationChangeRecorded>(ev =>
        {
            Revision = ev.Revision;
            _items[ev.ItemId].Changes.Add(ev.Change);
        });
        On<AccessRemediationVerified>(ev =>
        {
            Revision = ev.Revision;
            _items[ev.ItemId].Verification = ev.Verification;
        });
        On<AccessRemediationExceptionRecorded>(ev =>
        {
            Revision = ev.Revision;
            _items[ev.ItemId].Exception = ev.Exception;
        });
        On<AccessReviewResponsibilityReassigned>(ev =>
        {
            Revision = ev.Revision;
            _items[ev.Reassignment.ItemId].Reassignments.Add(ev.Reassignment);
        });
        On<AccessReviewCampaignCompleted>(ev =>
        {
            Revision = ev.Revision;
            Completion = ev.Completion;
        });
    }

    public AccessReviewCampaignLaunched? Launched { get; private set; }
    public AccessReviewCampaignCompletionView? Completion { get; private set; }
    public long Revision { get; private set; }
    public bool IsLaunched => Launched is not null;
    public string Status => Completion is null ? Active : Completed;

    public AccessReviewItemView? FindItem(Uuid itemId) =>
        _items.TryGetValue(itemId, out var state) ? state.Item : null;

    public AccessDecisionView? CurrentDecision(Uuid itemId) =>
        _items.TryGetValue(itemId, out var state) && state.Decisions.Count > 0
            ? state.Decisions[^1]
            : null;

    public bool HasDecision(Uuid decisionId) => _items.Values.Any(state =>
        state.Decisions.Any(decision => decision.DecisionId == decisionId));

    public bool IsReviewer(Uuid memberId) => _items.Keys.Any(itemId =>
        CurrentReviewerMemberId(itemId) == memberId);

    public Uuid? CurrentReviewerMemberId(Uuid itemId) =>
        _items.TryGetValue(itemId, out var state)
            ? state.Reassignments.LastOrDefault(reassignment =>
                reassignment.Responsibility == AccessReviewResponsibilityKind.Reviewer)?.AssignedMemberId
              ?? state.Item.ReviewerMemberId
            : null;

    public Uuid? CurrentRemediationOwnerMemberId(Uuid itemId) =>
        _items.TryGetValue(itemId, out var state)
            ? state.Reassignments.LastOrDefault(reassignment =>
                reassignment.Responsibility == AccessReviewResponsibilityKind.RemediationOwner)?.AssignedMemberId
              ?? Launched?.RemediationOwnerMemberId
            : null;

    public AccessReviewResponsibilityReassignmentView? FindResponsibilityReassignment(
        Uuid reassignmentId) => _items.Values.SelectMany(static state => state.Reassignments)
        .FirstOrDefault(reassignment => reassignment.ReassignmentId == reassignmentId);

    public Result<AccessReviewCampaignRegistration> Launch(string name, string instructions,
        DateTimeOffset deadline, Uuid snapshotId, string contentSha256,
        IReadOnlyList<AccessReviewerView> reviewers, IReadOnlyList<AccessReviewItemView> items,
        ActorReference launchedBy, DateTimeOffset now, Uuid? programId = null,
        Uuid? remediationOwnerMemberId = null)
    {
        if (Launched is { } launched)
            return launched.SnapshotId == snapshotId && launched.ProgramId == programId &&
                   launched.RemediationOwnerMemberId == remediationOwnerMemberId
                ? Result<AccessReviewCampaignRegistration>.Success(new(Id, launched.SnapshotId,
                    launched.ContentSha256, launched.Items.Count))
                : Failure<AccessReviewCampaignRegistration>(RequestErrorKind.Conflict,
                    "The campaign already exists with different content.");
        if (!AccessReviewVocabulary.IsBoundedText(name, 200) ||
            !AccessReviewVocabulary.IsBoundedText(instructions, 8000))
            return Failure<AccessReviewCampaignRegistration>(RequestErrorKind.Validation,
                "A campaign requires a name of at most 200 and instructions of at most 8000 characters.");
        if (deadline <= now)
            return Failure<AccessReviewCampaignRegistration>(RequestErrorKind.Validation,
                "A campaign deadline must be in the future.");
        if (items.Count == 0)
            return Failure<AccessReviewCampaignRegistration>(RequestErrorKind.Validation,
                "The assigned populations have no effective access to review.");
        RaiseEvent(new AccessReviewCampaignLaunched(_tenantId, Id, name.Trim(), instructions.Trim(),
            deadline, snapshotId, contentSha256, reviewers, items, launchedBy, now, programId,
            remediationOwnerMemberId));
        return Result<AccessReviewCampaignRegistration>.Success(new(Id, snapshotId, contentSha256,
            items.Count));
    }

    public Result<AccessReviewResponsibilityReassignmentView> ReassignResponsibility(
        Uuid itemId, string responsibility, long expectedRevision, Uuid reassignmentId,
        Uuid assignedMemberId, string reason, ActorReference reassignedBy,
        DateTimeOffset reassignedAt, string? delegationReason = null)
    {
        if (Launched is not { ProgramId: not null, RemediationOwnerMemberId: not null } ||
            !_items.TryGetValue(itemId, out var state))
            return Failure<AccessReviewResponsibilityReassignmentView>(RequestErrorKind.NotFound,
                "The review item was not found.");
        var existing = state.Reassignments.FirstOrDefault(reassignment =>
            reassignment.ReassignmentId == reassignmentId);
        if (existing is not null)
            return existing.Responsibility == responsibility &&
                   existing.AssignedMemberId == assignedMemberId && existing.Reason == reason.Trim() &&
                   existing.ReassignedBy == reassignedBy &&
                   existing.DelegationReason == delegationReason?.Trim()
                ? Result<AccessReviewResponsibilityReassignmentView>.Success(existing)
                : Failure<AccessReviewResponsibilityReassignmentView>(RequestErrorKind.Conflict,
                    "The reassignment request ID already has different content.");
        if (Completion is not null)
            return Failure<AccessReviewResponsibilityReassignmentView>(RequestErrorKind.Conflict,
                "A completed campaign cannot change.");
        if (expectedRevision != Revision)
            return Failure<AccessReviewResponsibilityReassignmentView>(RequestErrorKind.Conflict,
                $"The campaign is at revision {Revision}; expected {expectedRevision}.");
        if (!AccessReviewResponsibilityKind.IsKnown(responsibility) ||
            assignedMemberId == Uuid.Empty ||
            !AccessReviewVocabulary.IsBoundedText(reason, 2000) ||
            responsibility == AccessReviewResponsibilityKind.Reviewer &&
            !AccessReviewVocabulary.IsBoundedText(delegationReason, 2000) ||
            responsibility == AccessReviewResponsibilityKind.RemediationOwner &&
            delegationReason is not null ||
            reassignedBy.Kind != "member" ||
            !Uuid.TryParse(reassignedBy.Id, CultureInfo.InvariantCulture, out var actorMemberId) ||
            actorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(reassignedBy.Display) ||
            reassignedAt == default)
            return Failure<AccessReviewResponsibilityReassignmentView>(RequestErrorKind.Validation,
                "A reassignment requires a known responsibility, member, reason, actor, and timestamp.");
        if (responsibility == AccessReviewResponsibilityKind.Reviewer &&
            state.Decisions.Count > 0)
            return Failure<AccessReviewResponsibilityReassignmentView>(RequestErrorKind.Conflict,
                "A decided review item no longer needs a reviewer reassignment.");
        if (responsibility == AccessReviewResponsibilityKind.RemediationOwner &&
            RemediationStatus(state, reassignedAt) is not ("pending" or "provider_changed"))
            return Failure<AccessReviewResponsibilityReassignmentView>(RequestErrorKind.Conflict,
                "Only open remediation work can change its owner.");

        var previousMemberId = responsibility == AccessReviewResponsibilityKind.Reviewer
            ? CurrentReviewerMemberId(itemId)!.Value
            : CurrentRemediationOwnerMemberId(itemId)!.Value;
        var otherMemberId = responsibility == AccessReviewResponsibilityKind.Reviewer
            ? CurrentRemediationOwnerMemberId(itemId)
            : CurrentReviewerMemberId(itemId);
        if (previousMemberId == assignedMemberId)
            return Failure<AccessReviewResponsibilityReassignmentView>(RequestErrorKind.Conflict,
                "The responsibility is already assigned to this member.");
        if (otherMemberId == assignedMemberId)
            return Failure<AccessReviewResponsibilityReassignmentView>(RequestErrorKind.Validation,
                "The reviewer and remediation owner must remain distinct.");

        var reassignment = new AccessReviewResponsibilityReassignmentView(reassignmentId,
            responsibility, itemId, previousMemberId, assignedMemberId, reason.Trim(),
            reassignedBy, reassignedAt, delegationReason?.Trim());
        RaiseEvent(new AccessReviewResponsibilityReassigned(_tenantId, Id, Revision + 1,
            reassignment));
        return Result<AccessReviewResponsibilityReassignmentView>.Success(reassignment);
    }

    /// <summary>Explains why the reviewer cannot decide an item, or null when they can.</summary>
    public string? Eligibility(Uuid itemId, Uuid reviewerMemberId, Uuid reviewerUserId, bool bulk)
    {
        if (!_items.TryGetValue(itemId, out var state))
            return "not_found";
        if (CurrentReviewerMemberId(itemId) != reviewerMemberId)
            return "not_assigned";
        if (state.Item.SubjectUserId == reviewerUserId)
            return "self_review";
        return bulk && state.Item.Privileged ? "privileged_requires_individual_decision" : null;
    }

    public Result<IReadOnlyList<AccessDecisionView>> Decide(IReadOnlyList<Uuid> itemIds,
        long expectedRevision, Uuid decisionId, string decision, string rationale,
        Uuid reviewerMemberId, Uuid reviewerUserId, ActorReference decidedBy, DateTimeOffset now,
        string? bulkPreviewToken, SeparationOfDutiesWaiver? waiver)
    {
        if (Launched is null)
            return Failure<IReadOnlyList<AccessDecisionView>>(RequestErrorKind.NotFound,
                "The campaign was not found.");
        var replay = _items.Values.SelectMany(static state => state.Decisions)
            .Where(existing => existing.DecisionId == decisionId).ToArray();
        if (replay.Length > 0)
            return Result<IReadOnlyList<AccessDecisionView>>.Success(replay);
        if (Completion is not null)
            return Failure<IReadOnlyList<AccessDecisionView>>(RequestErrorKind.Conflict,
                "A completed campaign cannot change.");
        if (expectedRevision != Revision)
            return Failure<IReadOnlyList<AccessDecisionView>>(RequestErrorKind.Conflict,
                $"The campaign is at revision {Revision}; expected {expectedRevision}.");
        if (itemIds.Count == 0 || itemIds.Distinct().Count() != itemIds.Count)
            return Failure<IReadOnlyList<AccessDecisionView>>(RequestErrorKind.Validation,
                "A decision names one or more distinct items.");
        if (decision is null || !AccessReviewVocabulary.Decisions.Contains(decision) ||
            !AccessReviewVocabulary.IsBoundedText(rationale, 4000))
            return Failure<IReadOnlyList<AccessDecisionView>>(RequestErrorKind.Validation,
                "A decision is keep, modify, revoke, or unable_to_determine with a rationale of at most 4000 characters.");
        var bulk = bulkPreviewToken is not null;
        foreach (var itemId in itemIds)
        {
            var reason = Eligibility(itemId, reviewerMemberId, reviewerUserId, bulk);
            if (reason is "not_found" or "not_assigned")
                return Failure<IReadOnlyList<AccessDecisionView>>(RequestErrorKind.NotFound,
                    "The review item was not found among the reviewer's assignments.");
            if (reason == "self_review")
            {
                var scope = new SeparationOfDutiesWaiverScope(
                    SeparationOfDutiesRecordTypes.AccessReviewItem, itemId, Id, 1,
                    SeparationOfDutiesActions.Review);
                if (bulk || waiver is null || waiver.TenantId != _tenantId ||
                    !waiver.Allows(scope, reviewerMemberId, now))
                    return Failure<IReadOnlyList<AccessDecisionView>>(RequestErrorKind.Forbidden,
                        "A reviewer cannot decide their own access without an active exact-scope waiver.");
            }
            else if (reason is not null)
                return Failure<IReadOnlyList<AccessDecisionView>>(RequestErrorKind.Validation,
                    "Privileged access requires an individual named reviewer decision.");
            else if (waiver is not null)
                return Failure<IReadOnlyList<AccessDecisionView>>(RequestErrorKind.Forbidden,
                    "A separation-of-duties waiver may only be used for a current conflict.");
        }
        var afterDeadline = now > Launched.Deadline;
        var decisions = itemIds.Select(itemId => new AccessDecisionView(decisionId, itemId, decision,
            rationale.Trim(), reviewerMemberId, decidedBy, now, bulkPreviewToken, afterDeadline,
            waiver?.Id)).ToArray();
        RaiseEvent(new AccessDecisionsRecorded(_tenantId, Id, Revision + 1, decisions));
        return Result<IReadOnlyList<AccessDecisionView>>.Success(decisions);
    }

    public Result<AccessRemediationChangeView> RecordChange(Uuid itemId, long expectedRevision,
        Uuid changeId, string reference, string description, DateTimeOffset changedAt,
        ActorReference recordedBy, DateTimeOffset now)
    {
        if (RemediableItem(itemId, expectedRevision) is { } failure)
            return Result<AccessRemediationChangeView>.Failure(failure);
        if (_items[itemId].Changes.Find(change => change.ChangeId == changeId) is { } existing)
            return Result<AccessRemediationChangeView>.Success(existing);
        if (!AccessReviewVocabulary.IsBoundedText(reference, 500) ||
            !AccessReviewVocabulary.IsBoundedText(description, 4000) || changedAt > now)
            return Failure<AccessRemediationChangeView>(RequestErrorKind.Validation,
                "A provider change requires a reference, a description, and a change time that is not in the future.");
        var change = new AccessRemediationChangeView(changeId, reference.Trim(), description.Trim(),
            changedAt, recordedBy, now);
        RaiseEvent(new AccessRemediationChangeRecorded(_tenantId, Id, Revision + 1, itemId, change));
        return Result<AccessRemediationChangeView>.Success(change);
    }

    public Result<AccessRemediationVerificationView> Verify(Uuid itemId, long expectedRevision,
        AccessRemediationVerificationView verification)
    {
        if (_items.TryGetValue(itemId, out var state) &&
            state.Verification is { } existing && existing.SnapshotId == verification.SnapshotId)
            return Result<AccessRemediationVerificationView>.Success(existing);
        if (RemediableItem(itemId, expectedRevision) is { } failure)
            return Result<AccessRemediationVerificationView>.Failure(failure);
        if (verification.ObservedAt <= CurrentDecision(itemId)!.DecidedAt)
            return Failure<AccessRemediationVerificationView>(RequestErrorKind.Conflict,
                "Verification requires a population observed after the decision.");
        RaiseEvent(new AccessRemediationVerified(_tenantId, Id, Revision + 1, itemId, verification));
        return Result<AccessRemediationVerificationView>.Success(verification);
    }

    public Result<AccessRemediationExceptionView> RecordException(Uuid itemId,
        long expectedRevision, Uuid exceptionId, string rationale, DateTimeOffset? expiresAt,
        Uuid approverMemberId, ActorReference approvedBy, DateTimeOffset now)
    {
        if (_items.TryGetValue(itemId, out var state) &&
            state.Exception is { } existing && existing.ExceptionId == exceptionId)
            return Result<AccessRemediationExceptionView>.Success(existing);
        if (RemediableItem(itemId, expectedRevision) is { } failure)
            return Result<AccessRemediationExceptionView>.Failure(failure);
        if (CurrentReviewerMemberId(itemId) == approverMemberId)
            return Failure<AccessRemediationExceptionView>(RequestErrorKind.Forbidden,
                "The item's reviewer cannot approve an exception to its remediation.");
        if (!AccessReviewVocabulary.IsBoundedText(rationale, 4000) || expiresAt <= now)
            return Failure<AccessRemediationExceptionView>(RequestErrorKind.Validation,
                "A remediation exception requires a rationale of at most 4000 characters and a future expiry.");
        var exception = new AccessRemediationExceptionView(exceptionId, rationale.Trim(), expiresAt,
            approvedBy, now);
        RaiseEvent(new AccessRemediationExceptionRecorded(_tenantId, Id, Revision + 1, itemId,
            exception));
        return Result<AccessRemediationExceptionView>.Success(exception);
    }

    /// <summary>Lists the reasons the campaign cannot complete at <paramref name="now" />.</summary>
    public IReadOnlyList<string> CompletionBlockers(DateTimeOffset now)
    {
        var blockers = new List<string>();
        foreach (var itemId in _order)
        {
            var state = _items[itemId];
            if (state.Decisions.Count == 0)
                blockers.Add($"Item {itemId} is unresolved.");
            else if (RemediationStatus(state, now) == "pending" ||
                     RemediationStatus(state, now) == "provider_changed")
                blockers.Add($"Item {itemId} requires verified remediation or an approved exception.");
        }
        return blockers;
    }

    public Result<AccessReviewCampaignCompletionView> Complete(long expectedRevision,
        Uuid snapshotId, string contentSha256, string attestation, ActorReference completedBy,
        DateTimeOffset now)
    {
        if (Launched is null)
            return Failure<AccessReviewCampaignCompletionView>(RequestErrorKind.NotFound,
                "The campaign was not found.");
        if (Completion is { } completed)
            return completed.SnapshotId == snapshotId
                ? Result<AccessReviewCampaignCompletionView>.Success(completed)
                : Failure<AccessReviewCampaignCompletionView>(RequestErrorKind.Conflict,
                    "The campaign was already completed.");
        if (expectedRevision != Revision)
            return Failure<AccessReviewCampaignCompletionView>(RequestErrorKind.Conflict,
                $"The campaign is at revision {Revision}; expected {expectedRevision}.");
        if (CompletionBlockers(now) is { Count: > 0 } blockers)
            return Failure<AccessReviewCampaignCompletionView>(RequestErrorKind.Conflict,
                string.Join(" ", blockers.Take(10)));
        if (!AccessReviewVocabulary.IsBoundedText(attestation, 4000))
            return Failure<AccessReviewCampaignCompletionView>(RequestErrorKind.Validation,
                "Completion requires an attestation of at most 4000 characters.");
        var completion = new AccessReviewCampaignCompletionView(snapshotId, contentSha256,
            attestation.Trim(), completedBy, now);
        RaiseEvent(new AccessReviewCampaignCompleted(_tenantId, Id, Revision + 1, completion));
        return Result<AccessReviewCampaignCompletionView>.Success(completion);
    }

    public IReadOnlyList<AccessReviewItemStateView> ItemStates(DateTimeOffset now,
        Func<AccessReviewItemView, bool>? visible = null) =>
        _order.Select(itemId => _items[itemId])
            .Where(state => visible is null || visible(state.Item))
            .Select(state => new AccessReviewItemStateView(state.Item,
                state.Decisions.Count == 0 ? "unresolved" : "decided",
                state.Decisions.Count == 0 ? null : state.Decisions[^1], [.. state.Decisions],
                RemediationStatus(state, now), [.. state.Changes], state.Verification,
                state.Exception, CurrentReviewerMemberId(state.Item.ItemId),
                CurrentRemediationOwnerMemberId(state.Item.ItemId), [.. state.Reassignments]))
            .ToArray();

    public AccessReviewCampaignView ToView(DateTimeOffset now,
        Func<AccessReviewItemView, bool>? visible = null)
    {
        var launched = Launched ?? throw new InvalidOperationException("The campaign was not launched.");
        var items = ItemStates(now, visible);
        return new AccessReviewCampaignView(_tenantId, Id, Revision, launched.Name,
            launched.Instructions, launched.Deadline, Status, launched.SnapshotId,
            launched.ContentSha256, launched.Reviewers.Where(reviewer => visible is null ||
                items.Any(item => item.Item.PopulationId == reviewer.PopulationId)).ToArray(),
            items, items.Count(static item => item.Status == "unresolved"),
            items.Count(static item => item.RemediationStatus is "pending" or "provider_changed"),
            launched.LaunchedBy, launched.LaunchedAt, Completion, launched.ProgramId,
            launched.RemediationOwnerMemberId);
    }

    static string RemediationStatus(ItemState state, DateTimeOffset now)
    {
        var decision = state.Decisions.Count == 0 ? null : state.Decisions[^1];
        if (!AccessReviewVocabulary.RequiresRemediation(decision?.Decision))
            return "not_required";
        if (state.Verification is not null)
            return "verified";
        if (state.Exception is { } exception && (exception.ExpiresAt is null || now < exception.ExpiresAt))
            return "excepted";
        return state.Changes.Count > 0 ? "provider_changed" : "pending";
    }

    RequestError? RemediableItem(Uuid itemId, long expectedRevision)
    {
        if (Launched is null || !_items.ContainsKey(itemId))
            return new RequestError(RequestErrorKind.NotFound, "The review item was not found.");
        if (Completion is not null)
            return new RequestError(RequestErrorKind.Conflict, "A completed campaign cannot change.");
        if (expectedRevision != Revision)
            return new RequestError(RequestErrorKind.Conflict,
                $"The campaign is at revision {Revision}; expected {expectedRevision}.");
        return AccessReviewVocabulary.RequiresRemediation(CurrentDecision(itemId)?.Decision)
            ? null
            : new RequestError(RequestErrorKind.Conflict,
                "Only an item decided modify or revoke requires remediation.");
    }

    static Result<T> Failure<T>(RequestErrorKind kind, string message) =>
        Result<T>.Failure(new RequestError(kind, message));

    sealed class ItemState(AccessReviewItemView item)
    {
        public AccessReviewItemView Item { get; } = item;
        public List<AccessDecisionView> Decisions { get; } = [];
        public List<AccessRemediationChangeView> Changes { get; } = [];
        public List<AccessReviewResponsibilityReassignmentView> Reassignments { get; } = [];
        public AccessRemediationVerificationView? Verification { get; set; }
        public AccessRemediationExceptionView? Exception { get; set; }
    }
}
