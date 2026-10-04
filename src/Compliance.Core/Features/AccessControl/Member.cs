using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class Member : Aggregate
{
    readonly Uuid _tenantId;
    Uuid _userId;
    bool _isRegistered;
    string? _affiliation;
    Uuid _membershipEpisodeId;
    bool _isSuspended;
    bool _isDeprovisioned;
    bool _isDeprovisionCleanupComplete;
    Uuid _deprovisionedByMemberId;
    string? _deprovisionedByDisplay;
    DateTimeOffset? _deprovisionedAt;
    string? _deprovisionReason;

    public bool IsRegistered => _isRegistered;
    public string? Affiliation => _affiliation;
    public Uuid UserId => _userId;
    public Uuid MembershipEpisodeId => _membershipEpisodeId;
    public bool IsSuspended => _isSuspended;
    public bool IsDeprovisioned => _isDeprovisioned;
    public bool IsDeprovisionCleanupComplete => _isDeprovisionCleanupComplete;
    public Uuid DeprovisionedByMemberId => _deprovisionedByMemberId;
    public string? DeprovisionedByDisplay => _deprovisionedByDisplay;
    public DateTimeOffset? DeprovisionedAt => _deprovisionedAt;
    public string? DeprovisionReason => _deprovisionReason;

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
            _userId = registered.UserId;
            _membershipEpisodeId = registered.MembershipEpisodeId;
            _isSuspended = false;
            _isDeprovisioned = false;
            _isDeprovisionCleanupComplete = false;
        });
        On<MemberSuspended>(_ => _isSuspended = true);
        On<MemberReinstated>(_ => _isSuspended = false);
        On<MemberDeprovisioned>(deprovisioned =>
        {
            _isRegistered = false;
            _isSuspended = false;
            _isDeprovisioned = true;
            _isDeprovisionCleanupComplete = false;
            _deprovisionedByMemberId = deprovisioned.DeprovisionedByMemberId;
            _deprovisionedByDisplay = deprovisioned.DeprovisionedByDisplay;
            _deprovisionedAt = deprovisioned.DeprovisionedAt;
            _deprovisionReason = deprovisioned.Reason;
        });
        On<MemberDeprovisionCleanupCompleted>(_ => _isDeprovisionCleanupComplete = true);
    }

    public Result Register(string affiliation = "client_personnel")
    {
        EnsureUserIdentity();
        if (affiliation is not ("client_personnel" or "firm_staff"))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "Invalid membership affiliation."));
        if (_isDeprovisioned && !_isDeprovisionCleanupComplete)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Membership authority cleanup must finish before a new invitation can take effect."));
        if (!_isRegistered)
            RaiseEvent(new MemberRegistered(_tenantId, Id, _userId, affiliation,
                Uuid.CreateVersion4()));
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

    public Result Deprovision(Uuid actorMemberId, string actorDisplay, DateTimeOffset deprovisionedAt,
        string reason)
    {
        EnsureUserIdentity();
        if (_isDeprovisioned)
            return Result.Success;
        if (!_isRegistered)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The tenant member was not found."));
        if (actorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A member identity is required for the deprovision history."));
        if (deprovisionedAt == default)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A deprovision timestamp is required."));
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Enter a deprovision reason of at most 1000 characters."));

        RaiseEvent(new MemberDeprovisioned(_tenantId, Id, _userId, actorMemberId,
            actorDisplay.Trim(), deprovisionedAt, reason.Trim()));
        return Result.Success;
    }

    public Result CompleteDeprovisionCleanup(DateTimeOffset completedAt)
    {
        EnsureUserIdentity();
        if (!_isDeprovisioned)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Membership authority cleanup requires a deprovisioned member."));
        if (_isDeprovisionCleanupComplete)
            return Result.Success;
        if (completedAt == default)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A cleanup completion timestamp is required."));

        RaiseEvent(new MemberDeprovisionCleanupCompleted(_tenantId, Id, completedAt));
        return Result.Success;
    }

    void EnsureUserIdentity()
    {
        if (_userId == Uuid.Empty)
            throw new InvalidOperationException(
                "A member opened for verification cannot record membership changes.");
    }
}
