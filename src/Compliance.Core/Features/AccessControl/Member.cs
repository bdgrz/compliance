using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class Member : Aggregate
{
    readonly Uuid _tenantId;
    readonly Uuid _userId;
    bool _isRegistered;
    string? _affiliation;
    bool _isSuspended;

    public bool IsRegistered => _isRegistered;
    public string? Affiliation => _affiliation;
    public bool IsSuspended => _isSuspended;

    public Member(Uuid tenantId, Uuid userId)
        : this(tenantId, RbacIds.Member(tenantId, userId), userId)
    {
    }

    /// <summary>
    ///     Opens a member stream by member identity for read-only verification, such as checking
    ///     that a responsibility holder is still active. Commands require the user constructor.
    /// </summary>
    public static Member ForVerification(Uuid tenantId, Uuid memberId) =>
        new(tenantId, memberId, Uuid.Empty);

    Member(Uuid tenantId, Uuid memberId, Uuid userId)
        : base(memberId,
            new EventStreamAddress(tenantId.ToString(), "rbac-members", memberId.ToString()))
    {
        _tenantId = tenantId;
        _userId = userId;
        On<MemberRegistered>(registered =>
        {
            _isRegistered = true;
            _affiliation = registered.Affiliation;
        });
        On<MemberSuspended>(_ => _isSuspended = true);
        On<MemberReinstated>(_ => _isSuspended = false);
    }

    public Result Register(string affiliation = "client_personnel")
    {
        EnsureUserIdentity();
        if (affiliation is not ("client_personnel" or "firm_staff"))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "Invalid membership affiliation."));
        if (!_isRegistered)
            RaiseEvent(new MemberRegistered(_tenantId, Id, _userId, affiliation));
        else if (_affiliation != affiliation)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "This membership has a different affiliation."));
        return Result.Success;
    }

    public Result Suspend(Uuid actorMemberId, string actorDisplay, DateTimeOffset suspendedAt,
        string reason)
    {
        EnsureUserIdentity();
        if (!_isRegistered)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The tenant member was not found."));
        if (_isSuspended)
            return Result.Success;
        if (actorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A member identity is required for the suspension history."));
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Enter a suspension reason of at most 1000 characters."));

        RaiseEvent(new MemberSuspended(_tenantId, Id, _userId, actorMemberId,
            actorDisplay.Trim(), suspendedAt, reason.Trim()));
        return Result.Success;
    }

    public Result Reinstate(Uuid actorMemberId, string actorDisplay, DateTimeOffset reinstatedAt)
    {
        EnsureUserIdentity();
        if (!_isRegistered)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The tenant member was not found."));
        if (!_isSuspended)
            return Result.Success;
        if (actorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A member identity is required for the reinstatement history."));

        RaiseEvent(new MemberReinstated(_tenantId, Id, _userId, _affiliation!, actorMemberId,
            actorDisplay.Trim(), reinstatedAt));
        return Result.Success;
    }

    void EnsureUserIdentity()
    {
        if (_userId == Uuid.Empty)
            throw new InvalidOperationException(
                "A member opened for verification cannot record membership changes.");
    }
}
