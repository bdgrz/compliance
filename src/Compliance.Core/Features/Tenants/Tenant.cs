using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class Tenant : Aggregate
{
    string? _slug;
    TenantSlugState _slugState;
    bool _suspended;
    bool _requiresInvitation;
    bool _activated;
    string? _firstAdministratorEmail;
    Uuid _operatorUserId;
    string? _pendingSlug;
    readonly HashSet<string> _previousSlugs = new(StringComparer.Ordinal);

    public Tenant(Uuid id)
        : base(id, new EventStreamAddress("bdgrz", "tenants", id.ToString()))
    {
        On<TenantRegistered>(Apply);
        On<TenantSlugConfirmed>(Apply);
        On<TenantSlugRejected>(Apply);
        On<TenantSlugSurrenderRequested>(Apply);
        On<TenantSlugSurrenderConfirmed>(Apply);
        On<TenantSlugSurrenderFailed>(Apply);
        On<TenantSuspended>(_ => _suspended = true);
        On<TenantReactivated>(_ => _suspended = false);
        On<TenantActivated>(_ => _activated = true);
        On<TenantSlugChangeRequested>(ev => _pendingSlug = ev.NewSlug);
        On<TenantSlugChanged>(ev =>
        {
            _previousSlugs.Add(ev.OldSlug);
            _slug = ev.NewSlug;
            _pendingSlug = null;
        });
        On<TenantSlugChangeRejected>(_ => _pendingSlug = null);
    }

    public bool IsActive => _slugState == TenantSlugState.Confirmed && !_suspended &&
                            (!_requiresInvitation || _activated);
    public bool IsRegistered => _slug is not null && _slugState != TenantSlugState.Rejected;
    public string? CurrentSlug => _slug;
    public Uuid OperatorUserId => _operatorUserId;
    public bool IsSuspended => _suspended;

    public Result<TenantRegistration> Register(Uuid ownerUserId, string name, string slug,
        string? legalName = null, string? firstAdministratorEmail = null,
        bool creatorIsAdministrator = false)
    {
        if (ownerUserId == Uuid.Empty)
            return Failure<TenantRegistration>(RequestErrorKind.Validation, "A tenant requires an owner.");
        if (string.IsNullOrWhiteSpace(name))
            return Failure<TenantRegistration>(RequestErrorKind.Validation, "A tenant requires a name.");
        if (!TenantSlugs.TryNormalize(slug, out var normalizedSlug, out var slugReason))
            return Failure<TenantRegistration>(RequestErrorKind.Validation, slugReason);
        string? normalizedEmail = null;
        if (firstAdministratorEmail is not null &&
            !EmailAddresses.TryNormalize(firstAdministratorEmail, out normalizedEmail))
            return Failure<TenantRegistration>(RequestErrorKind.Validation, "Enter a valid first administrator email address.");
        if (creatorIsAdministrator && normalizedEmail is null)
            return Failure<TenantRegistration>(RequestErrorKind.Validation,
                "A verified creator email is required for self-service registration.");
        if (_slug is not null)
            return Failure<TenantRegistration>(RequestErrorKind.Conflict, "The tenant is already registered.");

        RaiseEvent(new TenantRegistered(Id, ownerUserId, name.Trim(), normalizedSlug,
            string.IsNullOrWhiteSpace(legalName) ? name.Trim() : legalName.Trim(),
            normalizedEmail, creatorIsAdministrator));
        return Result<TenantRegistration>.Success(new TenantRegistration(Id, normalizedSlug));
    }

    public Result Activate(Uuid firstAdministratorUserId, string firstAdministratorEmail)
    {
        if (!_requiresInvitation || _slugState != TenantSlugState.Confirmed)
            return Failure(RequestErrorKind.Conflict, "The tenant is not ready to activate.");
        if (_activated)
            return Result.Success;
        if (firstAdministratorUserId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation, "A first administrator is required.");
        if (!EmailAddresses.TryNormalize(firstAdministratorEmail, out var normalized) ||
            normalized != _firstAdministratorEmail)
            return Failure(RequestErrorKind.Forbidden, "Only the invited first administrator may activate.");
        RaiseEvent(new TenantActivated(Id, firstAdministratorUserId));
        return Result.Success;
    }

    public Result ConfirmSlug(string slug)
    {
        if (_pendingSlug is null && _slugState == TenantSlugState.Confirmed && Matches(slug))
            return Result.Success;
        if (_pendingSlug is not null && MatchesPendingChange(slug))
        {
            RaiseEvent(new TenantSlugChanged(Id, _slug!, _pendingSlug));
            return Result.Success;
        }
        if (!MatchesPending(slug))
            return Failure(RequestErrorKind.Conflict, "The tenant is not awaiting that slug.");
        RaiseEvent(new TenantSlugConfirmed(Id, _slug!));
        return Result.Success;
    }

    public Result RejectSlug(string slug)
    {
        if (_pendingSlug is not null && MatchesPendingChange(slug))
        {
            RaiseEvent(new TenantSlugChangeRejected(Id, _slug!, _pendingSlug));
            return Result.Success;
        }
        if (!MatchesPending(slug))
            return Failure(RequestErrorKind.Conflict, "The tenant is not awaiting that slug.");
        RaiseEvent(new TenantSlugRejected(Id, _slug!));
        return Result.Success;
    }

    public Result RequestSlugChange(string slug)
    {
        if (!IsActive)
            return Failure(RequestErrorKind.Conflict, "The tenant is not active.");
        if (!TenantSlugs.TryNormalize(slug, out var normalized, out var reason))
            return Failure(RequestErrorKind.Validation, reason);
        if (normalized == _slug)
            return Result.Success;
        if (_pendingSlug is not null)
            return Failure(RequestErrorKind.Conflict, "A slug change is already pending.");
        RaiseEvent(new TenantSlugChangeRequested(Id, _slug!, normalized));
        return Result.Success;
    }

    public Result RequestSlugSurrender(string slug)
    {
        if (_slugState != TenantSlugState.Confirmed || !Matches(slug))
            return Failure(RequestErrorKind.Conflict, "The tenant does not own that slug.");
        RaiseEvent(new TenantSlugSurrenderRequested(Id, _slug!));
        return Result.Success;
    }

    public Result Suspend(Uuid operatorUserId)
    {
        if (_slug is null || _slugState == TenantSlugState.Rejected)
            return Failure(RequestErrorKind.NotFound, "The tenant does not exist.");
        if (_suspended)
            return Result.Success;
        if (!IsActive)
            return Failure(RequestErrorKind.Conflict, "The tenant is not active.");
        RaiseEvent(new TenantSuspended(Id, operatorUserId));
        return Result.Success;
    }

    public Result Reactivate(Uuid operatorUserId)
    {
        if (_slug is null || _slugState == TenantSlugState.Rejected)
            return Failure(RequestErrorKind.NotFound, "The tenant does not exist.");
        if (_slugState != TenantSlugState.Confirmed || _requiresInvitation && !_activated)
            return Failure(RequestErrorKind.Conflict, "The tenant is not ready to reactivate.");
        if (_suspended)
            RaiseEvent(new TenantReactivated(Id, operatorUserId));
        return Result.Success;
    }

    public Result ConfirmSlugSurrender(string slug)
    {
        if (_previousSlugs.Contains(slug))
            return Result.Success;
        if (_slugState != TenantSlugState.Surrendering || !Matches(slug))
            return Failure(RequestErrorKind.Conflict, "The tenant is not surrendering that slug.");
        RaiseEvent(new TenantSlugSurrenderConfirmed(Id, _slug!));
        return Result.Success;
    }

    public Result RejectSlugSurrender(string slug)
    {
        if (_slugState != TenantSlugState.Surrendering || !Matches(slug))
            return Failure(RequestErrorKind.Conflict, "The tenant is not surrendering that slug.");
        RaiseEvent(new TenantSlugSurrenderFailed(Id, _slug!));
        return Result.Success;
    }

    bool Matches(string slug) => TenantSlugs.TryNormalize(slug, out var normalized) && normalized == _slug;
    bool MatchesPending(string slug) => _slugState == TenantSlugState.Pending && Matches(slug);
    bool MatchesPendingChange(string slug) =>
        TenantSlugs.TryNormalize(slug, out var normalized) && normalized == _pendingSlug;

    void Apply(TenantRegistered registered)
    {
        _slug = registered.Slug;
        _slugState = TenantSlugState.Pending;
        _requiresInvitation = registered.FirstAdministratorEmail is not null &&
                              !registered.CreatorIsAdministrator;
        _firstAdministratorEmail = registered.FirstAdministratorEmail;
        _operatorUserId = registered.CreatorIsAdministrator ? Uuid.Empty : registered.OwnerUserId;
    }
    void Apply(TenantSlugConfirmed _) => _slugState = TenantSlugState.Confirmed;
    void Apply(TenantSlugRejected _) => _slugState = TenantSlugState.Rejected;
    void Apply(TenantSlugSurrenderRequested _) => _slugState = TenantSlugState.Surrendering;
    void Apply(TenantSlugSurrenderConfirmed _) { _slug = null; _slugState = TenantSlugState.None; }
    void Apply(TenantSlugSurrenderFailed _) => _slugState = TenantSlugState.Confirmed;

    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
    static Result<T> Failure<T>(RequestErrorKind kind, string message) => Result<T>.Failure(new RequestError(kind, message));

    enum TenantSlugState { None, Pending, Confirmed, Rejected, Surrendering }
}
