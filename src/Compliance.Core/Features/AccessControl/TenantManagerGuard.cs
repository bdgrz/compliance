using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The tenant-scoped serialization point for changes that can remove an active tenant
///     administrator (#432). Every such change records its withdrawal here, against the sequence
///     its decision observed, before the member or team-member stream changes. Two concurrent
///     decisions therefore cannot both rely on each other as the remaining administrator.
/// </summary>
public sealed class TenantManagerGuard : Aggregate
{
    readonly Uuid _tenantId;
    readonly Dictionary<Uuid, HashSet<string>> _withdrawn = [];

    public TenantManagerGuard(Uuid tenantId)
        : base(tenantId, new EventStreamAddress(tenantId.ToString(), "rbac-manager-guard",
            tenantId.ToString()))
    {
        _tenantId = tenantId;
        On<TenantManagerWithdrawn>(withdrawn =>
        {
            if (!_withdrawn.TryGetValue(withdrawn.MemberId, out var reasons))
                _withdrawn[withdrawn.MemberId] = reasons = new HashSet<string>(StringComparer.Ordinal);
            reasons.Add(withdrawn.Reason);
            Sequence++;
        });
        On<TenantManagerRestored>(restored =>
        {
            if (_withdrawn.TryGetValue(restored.MemberId, out var reasons) &&
                reasons.Remove(restored.Reason) && reasons.Count == 0)
                _withdrawn.Remove(restored.MemberId);
            Sequence++;
        });
    }

    /// <summary>The number of guard decisions applied; a decision must still match it to commit.</summary>
    public long Sequence { get; private set; }

    public bool IsWithdrawn(Uuid memberId) => _withdrawn.ContainsKey(memberId);

    /// <summary>
    ///     Records a withdrawal decided at <paramref name="observedSequence" />. A later guard
    ///     decision makes this one stale (transient); a withdrawal without another active
    ///     administrator is refused.
    /// </summary>
    public Result Withdraw(Uuid memberId, string reason, long observedSequence,
        bool anotherAdministratorRemains)
    {
        if (observedSequence != Sequence)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Another tenant manager change committed first.", isTransient: true));
        if (_withdrawn.TryGetValue(memberId, out var reasons) && reasons.Contains(reason))
            return Result.Success;
        if (!anotherAdministratorRemains)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "This change would leave the tenant without an active administrator."));
        RaiseEvent(new TenantManagerWithdrawn(_tenantId, memberId, reason));
        return Result.Success;
    }

    public Result Restore(Uuid memberId, string reason)
    {
        if (_withdrawn.TryGetValue(memberId, out var reasons) && reasons.Contains(reason))
            RaiseEvent(new TenantManagerRestored(_tenantId, memberId, reason));
        return Result.Success;
    }
}
