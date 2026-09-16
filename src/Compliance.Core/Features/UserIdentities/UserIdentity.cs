using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>An external identity provider association for a platform user.</summary>
public sealed class UserIdentity : Aggregate
{
    static Uuid IdentityNamespaceId { get; } =
        Uuid.Parse("17729a2d-d41d-5c56-953a-d3fe810cc8d2", CultureInfo.InvariantCulture);

    readonly string _provider;
    readonly string _identifier;
    bool _isRegistered;
    Uuid _userId;
    string? _emailAddress;

    public UserIdentity(string provider, string identifier)
        : base(
            CreateIdentityId(provider, identifier),
            new EventStreamAddress(
                "bdgrz",
                "user-identities",
                CreateIdentityId(provider, identifier).ToString()))
    {
        _provider = provider;
        _identifier = identifier;
        On<UserIdentityRegistered>(Apply);
    }

    static Uuid CreateIdentityId(string provider, string identifier) =>
        Uuid.CreateVersion5(IdentityNamespaceId, $"{provider}\n{identifier}");

    public bool IsRegistered => _isRegistered;

    public Result<AuthenticatedUserIdentity> Register(
        Uuid? userId,
        string? emailAddress)
    {
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
        if (!_isRegistered)
        {
            return Result<AuthenticatedUserIdentity>.Failure(new RequestError(
                RequestErrorKind.NotFound,
                "The user identity is not registered."));
        }

        AuditEvent(new UserIdentityAuthenticated(_userId, _provider, _identifier));
        return Result<AuthenticatedUserIdentity>.Success(
            new AuthenticatedUserIdentity(Id, _userId, _emailAddress));
    }

    void Apply(UserIdentityRegistered registered)
    {
        if (_isRegistered ||
            !string.Equals(registered.Provider, _provider, StringComparison.Ordinal) ||
            !string.Equals(registered.Identifier, _identifier, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The user identity stream does not match its provider identity.");
        }

        _isRegistered = true;
        _userId = registered.UserId;
        _emailAddress = registered.EmailAddress;
    }

    Result<AuthenticatedUserIdentity> Registered() =>
        Result<AuthenticatedUserIdentity>.Success(new AuthenticatedUserIdentity(Id, _userId, _emailAddress));
}
