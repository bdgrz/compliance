using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class Member : Aggregate
{
    readonly Uuid _tenantId;
    readonly Uuid _userId;
    bool _isRegistered;

    public Member(Uuid tenantId, Uuid userId)
        : base(
            RbacIds.Member(tenantId, userId),
            new EventStreamAddress(tenantId.ToString(), "rbac-members", RbacIds.Member(tenantId, userId).ToString()))
    {
        _tenantId = tenantId;
        _userId = userId;
        On<MemberRegistered>(_ => _isRegistered = true);
    }

    public Result Register()
    {
        if (!_isRegistered)
            RaiseEvent(new MemberRegistered(_tenantId, Id, _userId));
        return Result.Success;
    }
}
