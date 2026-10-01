using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
/// Owns the append-only, effective-dated access-review scope decisions for one system instance
/// (M0-D05). A new decision supersedes; it never edits a prior one.
/// </summary>
public sealed class SystemInstanceAccessReviewScope : Aggregate
{
    public const string Included = "included";
    public const string Excluded = "excluded";

    readonly Uuid _tenantId;
    readonly List<AccessReviewScopeDecisionView> _decisions = [];

    public IReadOnlyList<AccessReviewScopeDecisionView> Decisions => _decisions;

    public SystemInstanceAccessReviewScope(Uuid tenantId, Uuid systemInstanceId)
        : base(systemInstanceId, new EventStreamAddress(tenantId.ToString(),
            AccessReviewScopeStreams.Area, systemInstanceId.ToString()))
    {
        _tenantId = tenantId;
        On<AccessReviewScopeDecided>(ev => _decisions.Add(new AccessReviewScopeDecisionView(
            ev.TenantId, ev.ApplicationId, ev.SystemInstanceId, ev.SystemInstanceRevision,
            ev.DecisionId, ev.Sequence, ev.Decision, ev.Reason, ev.EffectiveFrom, ev.ReviewBy,
            ev.Actor, ev.DecidedAt, ev.SeparationOfDutiesWaiverId)));
    }

    /// <summary>Returns the latest decision whose effective date is not after <paramref name="asOf"/>.</summary>
    public AccessReviewScopeDecisionView? EffectiveAt(DateTimeOffset asOf) =>
        AccessReviewScopeStatus.EffectiveAt(_decisions, asOf);

    public AccessReviewScopeView ToView(Uuid applicationId, DateTimeOffset asOf)
    {
        var effective = EffectiveAt(asOf);
        return new AccessReviewScopeView(_tenantId, applicationId, Id, asOf,
            effective?.Decision ?? AccessReviewScopeStatus.Unresolved, effective, [.. _decisions]);
    }

    public Result<AccessReviewScopeDecisionView> Decide(DeclaredSystemInstance instance,
        long expectedDecisionCount, Uuid decisionId, string decision, string reason,
        DateTimeOffset effectiveFrom, DateTimeOffset? reviewBy, Uuid approverMemberId,
        string approverDisplay, DateTimeOffset decidedAt, SeparationOfDutiesWaiver? waiver)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (!instance.IsCreated)
            return Result<AccessReviewScopeDecisionView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The system instance was not found."));
        return Decide(ScopedSystemInstance.From(instance), expectedDecisionCount, decisionId,
            decision, reason, effectiveFrom, reviewBy, approverMemberId, approverDisplay,
            decidedAt, waiver);
    }

    /// <summary>
    /// Decides scope for a registered or legacy application-stream instance at its exact revision.
    /// </summary>
    public Result<AccessReviewScopeDecisionView> Decide(ScopedSystemInstance instance,
        long expectedDecisionCount, Uuid decisionId, string decision, string reason,
        DateTimeOffset effectiveFrom, DateTimeOffset? reviewBy, Uuid approverMemberId,
        string approverDisplay, DateTimeOffset decidedAt, SeparationOfDutiesWaiver? waiver)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var retry = _decisions.FirstOrDefault(existing => existing.DecisionId == decisionId);
        if (retry is not null)
            return Result<AccessReviewScopeDecisionView>.Success(retry);
        var failure = Validate(instance, expectedDecisionCount, decision, reason, effectiveFrom,
            reviewBy, approverMemberId, decidedAt, waiver);
        if (failure is not null)
            return Result<AccessReviewScopeDecisionView>.Failure(failure);
        var normalizedDisplay = approverDisplay.Trim();
        var ev = new AccessReviewScopeDecided(_tenantId, instance.ApplicationId, Id,
            instance.Revision, decisionId, _decisions.Count + 1, decision, reason.Trim(),
            effectiveFrom, reviewBy, approverMemberId, normalizedDisplay, decidedAt, waiver?.Id)
        {
            StoredActor = ActorReference.ForMember(approverMemberId, normalizedDisplay),
        };
        RaiseEvent(ev);
        return Result<AccessReviewScopeDecisionView>.Success(_decisions[^1]);
    }

    RequestError? Validate(ScopedSystemInstance instance, long expectedDecisionCount,
        string decision, string reason, DateTimeOffset effectiveFrom, DateTimeOffset? reviewBy,
        Uuid approverMemberId, DateTimeOffset decidedAt, SeparationOfDutiesWaiver? waiver)
    {
        if (instance.Id != Id)
            return new RequestError(RequestErrorKind.NotFound, "The system instance was not found.");
        if (instance.IsRetired)
            return new RequestError(RequestErrorKind.Conflict,
                "A retired system instance cannot receive a new scope decision.");
        if (decision is not (Included or Excluded))
            return new RequestError(RequestErrorKind.Validation,
                "The decision must be included or excluded.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000)
            return new RequestError(RequestErrorKind.Validation,
                "A scope decision requires a reason of at most 2000 characters.");
        if (reviewBy is { } review && review <= effectiveFrom)
            return new RequestError(RequestErrorKind.Validation,
                "The review date must be after the effective date.");
        if (expectedDecisionCount != _decisions.Count)
            return new RequestError(RequestErrorKind.Conflict,
                $"The instance has {_decisions.Count} scope decisions; expected {expectedDecisionCount}.");
        if (_decisions.Count > 0 && effectiveFrom < _decisions[^1].EffectiveFrom)
            return new RequestError(RequestErrorKind.Validation,
                "A new scope decision cannot take effect before the decision it supersedes.");

        // Separation of duties (M0-D03, M0-D05): the registrant and current access owner may
        // approve scope only with an independently approved exact-scope waiver.
        var conflict = approverMemberId == instance.RegisteredByMemberId ||
                       approverMemberId == instance.AccessOwnerMemberId;
        if (!conflict)
            return waiver is null ? null : new RequestError(RequestErrorKind.Forbidden,
                "A separation-of-duties waiver may only be used for a current conflict.");
        var scope = new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.SystemInstanceAccessReviewScope, Id, Id,
            instance.Revision, SeparationOfDutiesActions.Approve);
        return waiver is not null && waiver.TenantId == _tenantId &&
               waiver.Allows(scope, approverMemberId, decidedAt)
            ? null
            : new RequestError(RequestErrorKind.Forbidden,
                "The registrant or access owner cannot approve the system instance's access-review scope without an active exact-scope waiver.");
    }
}
