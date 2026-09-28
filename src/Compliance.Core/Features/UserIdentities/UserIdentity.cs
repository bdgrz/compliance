using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>An external identity provider association for a platform user.</summary>
public sealed class UserIdentity : Aggregate
{
    static Uuid IdentityNamespaceId { get; } =
        Uuid.Parse("17729a2d-d41d-5c56-953a-d3fe810cc8d2", CultureInfo.InvariantCulture);

    string? _provider;
    string? _identifier;
    bool _isRegistered;
    bool _isRevoked;
    Uuid _userId;
    Uuid _replacementIdentityId;
    string? _emailAddress;

    public UserIdentity(string provider, string identifier)
        : this(CreateIdentityId(provider, identifier), provider, identifier)
    {
    }

    /// <summary>Hydrates an identity from its stable identity ID, for session revocation checks.</summary>
    public UserIdentity(Uuid identityId)
        : this(identityId, null, null)
    {
    }

    UserIdentity(Uuid identityId, string? provider, string? identifier)
        : base(identityId, new EventStreamAddress("bdgrz", "user-identities", identityId.ToString()))
    {
        _provider = provider;
        _identifier = identifier;
        On<UserIdentityRegistered>(Apply);
        On<UserIdentityRevoked>(Apply);
    }

    static Uuid CreateIdentityId(string provider, string identifier) =>
        Uuid.CreateVersion5(IdentityNamespaceId, $"{provider}\n{identifier}");

    public static Uuid GetIdentityId(string provider, string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        return CreateIdentityId(provider, identifier);
    }

    public bool IsRegistered => _isRegistered;

    public bool IsRevoked => _isRevoked;

    public Uuid UserId => _userId;

    public Result<AuthenticatedUserIdentity> Register(
        Uuid? userId,
        string? emailAddress)
    {
        if (_provider is null || _identifier is null)
        {
            return Result<AuthenticatedUserIdentity>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "An identity must be registered from its provider and subject."));
        }

        if (userId == Uuid.Empty)
        {
            return Result<AuthenticatedUserIdentity>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "A user identity requires a user ID."));
        }

        if (_isRegistered)
        {
            return userId is not null && _userId != userId.Value
                ? Result<AuthenticatedUserIdentity>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The provider identity belongs to another user."))
                : Registered();
        }

        var registeredUserId = userId ?? Uuid.CreateVersion4();
        RaiseEvent(new UserIdentityRegistered(
            registeredUserId,
            _provider,
            _identifier,
            emailAddress));
        return Registered();
    }

    public Result<AuthenticatedUserIdentity> Authenticate()
    {
        if (_isRevoked)
        {
            return Result<AuthenticatedUserIdentity>.Failure(new RequestError(
                RequestErrorKind.Unauthorized,
                "The provider identity has been revoked."));
        }

        if (!_isRegistered || _provider is null || _identifier is null)
        {
            return Result<AuthenticatedUserIdentity>.Failure(new RequestError(
                RequestErrorKind.NotFound,
                "The user identity is not registered."));
        }

        AuditEvent(new UserIdentityAuthenticated(_userId, _provider, _identifier));
        return Result<AuthenticatedUserIdentity>.Success(
            new AuthenticatedUserIdentity(Id, _userId, _emailAddress));
    }

    public Result Revoke(Uuid replacementIdentityId, DateTimeOffset revokedAt)
    {
        if (!_isRegistered)
        {
            return Result.Failure(new RequestError(
                RequestErrorKind.NotFound,
                "The provider identity is not registered."));
        }

        if (replacementIdentityId == Uuid.Empty || replacementIdentityId == Id)
        {
            return Result.Failure(new RequestError(
                RequestErrorKind.Validation,
                "A provider identity must be replaced by a different registered identity."));
        }

        if (_isRevoked)
        {
            return _replacementIdentityId == replacementIdentityId
                ? Result.Success
                : Result.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The provider identity has already been replaced."));
        }

        RaiseEvent(new UserIdentityRevoked(Id, _userId, replacementIdentityId, revokedAt));
        return Result.Success;
    }

    void Apply(UserIdentityRegistered registered)
    {
        if (_isRegistered ||
            (_provider is not null && !string.Equals(registered.Provider, _provider, StringComparison.Ordinal)) ||
            (_identifier is not null && !string.Equals(registered.Identifier, _identifier, StringComparison.Ordinal)) ||
            CreateIdentityId(registered.Provider, registered.Identifier) != Id)
        {
            throw new InvalidOperationException("The user identity stream does not match its provider identity.");
        }

        _isRegistered = true;
        _provider = registered.Provider;
        _identifier = registered.Identifier;
        _userId = registered.UserId;
        _emailAddress = registered.EmailAddress;
    }

    void Apply(UserIdentityRevoked revoked)
    {
        if (!_isRegistered || _isRevoked || revoked.UserIdentityId != Id || revoked.UserId != _userId ||
            revoked.ReplacementIdentityId == Id)
        {
            throw new InvalidOperationException("The identity revocation does not match its registered identity.");
        }

        _isRevoked = true;
        _replacementIdentityId = revoked.ReplacementIdentityId;
    }

    Result<AuthenticatedUserIdentity> Registered() =>
        Result<AuthenticatedUserIdentity>.Success(new AuthenticatedUserIdentity(Id, _userId, _emailAddress));
}
