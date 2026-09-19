using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class Member : Aggregate
{
    readonly Uuid _tenantId;
    readonly Uuid _userId;
    bool _isRegistered;
    string? _affiliation;

    public Member(Uuid tenantId, Uuid userId)
        : base(
            RbacIds.Member(tenantId, userId),
            new EventStreamAddress(tenantId.ToString(), "rbac-members", RbacIds.Member(tenantId, userId).ToString()))
    {
        _tenantId = tenantId;
        _userId = userId;
        On<MemberRegistered>(registered =>
        {
            _isRegistered = true;
            _affiliation = registered.Affiliation;
        });
    }

    public Result Register(string affiliation = "client_personnel")
    {
        if (affiliation is not ("client_personnel" or "firm_staff"))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "Invalid membership affiliation."));
        if (!_isRegistered)
            RaiseEvent(new MemberRegistered(_tenantId, Id, _userId, affiliation));
        else if (_affiliation != affiliation)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "This membership has a different affiliation."));
        return Result.Success;
    }
}
