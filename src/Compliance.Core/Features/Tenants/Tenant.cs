using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class Tenant : Aggregate
{
    string? _slug;
    TenantSlugState _slugState;

    public Tenant(Uuid id)
        : base(id, new EventStreamAddress("bdgrz", "tenants", id.ToString()))
    {
        On<TenantRegistered>(Apply);
        On<TenantSlugConfirmed>(Apply);
        On<TenantSlugRejected>(Apply);
        On<TenantSlugSurrenderRequested>(Apply);
        On<TenantSlugSurrenderConfirmed>(Apply);
        On<TenantSlugSurrenderFailed>(Apply);
    }

    public Result<TenantRegistration> Register(Uuid ownerUserId, string name, string slug)
    {
        if (ownerUserId == Uuid.Empty)
            return Failure<TenantRegistration>(RequestErrorKind.Validation, "A tenant requires an owner.");
        if (string.IsNullOrWhiteSpace(name))
            return Failure<TenantRegistration>(RequestErrorKind.Validation, "A tenant requires a name.");
        if (!TenantSlugs.TryNormalize(slug, out var normalizedSlug))
            return Failure<TenantRegistration>(RequestErrorKind.Validation, "Enter a valid tenant slug.");
        if (_slug is not null)
            return Failure<TenantRegistration>(RequestErrorKind.Conflict, "The tenant is already registered.");

        RaiseEvent(new TenantRegistered(Id, ownerUserId, name.Trim(), normalizedSlug));
        return Result<TenantRegistration>.Success(new TenantRegistration(Id, normalizedSlug));
    }

    public Result ConfirmSlug(string slug)
    {
        if (!MatchesPending(slug))
            return Failure(RequestErrorKind.Conflict, "The tenant is not awaiting that slug.");
        RaiseEvent(new TenantSlugConfirmed(Id, _slug!));
        return Result.Success;
    }

    public Result RejectSlug(string slug)
    {
        if (!MatchesPending(slug))
            return Failure(RequestErrorKind.Conflict, "The tenant is not awaiting that slug.");
        RaiseEvent(new TenantSlugRejected(Id, _slug!));
        return Result.Success;
    }

    public Result RequestSlugSurrender(string slug)
    {
        if (_slugState != TenantSlugState.Confirmed || !Matches(slug))
            return Failure(RequestErrorKind.Conflict, "The tenant does not own that slug.");
        RaiseEvent(new TenantSlugSurrenderRequested(Id, _slug!));
        return Result.Success;
    }

    public Result ConfirmSlugSurrender(string slug)
    {
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

    void Apply(TenantRegistered registered) { _slug = registered.Slug; _slugState = TenantSlugState.Pending; }
    void Apply(TenantSlugConfirmed _) => _slugState = TenantSlugState.Confirmed;
    void Apply(TenantSlugRejected _) => _slugState = TenantSlugState.Rejected;
    void Apply(TenantSlugSurrenderRequested _) => _slugState = TenantSlugState.Surrendering;
    void Apply(TenantSlugSurrenderConfirmed _) { _slug = null; _slugState = TenantSlugState.None; }
    void Apply(TenantSlugSurrenderFailed _) => _slugState = TenantSlugState.Confirmed;

    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
    static Result<T> Failure<T>(RequestErrorKind kind, string message) => Result<T>.Failure(new RequestError(kind, message));

    enum TenantSlugState { None, Pending, Confirmed, Rejected, Surrendering }
}
