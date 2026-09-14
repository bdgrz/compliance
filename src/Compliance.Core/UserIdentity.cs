using Cntryl.Portia;

namespace Bdgrz.Compliance;

/// <summary>An external identity provider association for a platform user.</summary>
public sealed class UserIdentity : Aggregate
{
    public UserIdentity(Uuid id)
        : base(id, new EventStreamAddress("bdgrz", "user-identities", id.ToString()))
    {
        On<UserIdentityRegistered>(Apply);
    }

    public string? Provider { get; private set; }

    public Uuid UserId { get; private set; }

    public string? Identifier { get; private set; }

    public string? EmailAddress { get; private set; }

    public bool IsRegistered => Provider is not null;

    public Result Register(
        Uuid userId,
        string provider,
        string identifier,
        string? emailAddress)
    {
        if (IsRegistered)
        {
            return Result.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "The user identity is already registered."));
        }

        if (userId == Uuid.Empty)
        {
            return Result.Failure(new RequestError(
                RequestErrorKind.Validation,
                "A user identity requires a user ID."));
        }

        RaiseEvent(new UserIdentityRegistered(
            userId,
            provider,
            identifier,
            emailAddress));
        return Result.Success;
    }

    void Apply(UserIdentityRegistered registered)
    {
        UserId = registered.UserId;
        Provider = registered.Provider;
        Identifier = registered.Identifier;
        EmailAddress = registered.EmailAddress;
    }
}
