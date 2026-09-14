using System.Globalization;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance;

/// <summary>Development-only identity settings used by the registration handler.</summary>
public sealed record DeveloperUserRegistration(bool Enabled)
{
    public const string Provider = "bdgrz-development";

    public static Uuid IdentifierNamespaceId { get; } =
        Uuid.Parse("8b53bb9d-09b9-5f2b-934c-2f04296d64ee", CultureInfo.InvariantCulture);
}

public sealed class UserIdentityRegistration(IAggregateRepository repository)
{
    static Uuid IdentityNamespaceId { get; } =
        Uuid.Parse("17729a2d-d41d-5c56-953a-d3fe810cc8d2", CultureInfo.InvariantCulture);

    public async ValueTask<Result<RegisteredUserIdentity>> RegisterAsync(
        string provider,
        string identifier,
        string? emailAddress,
        Uuid? existingUserId,
        IExecutionContext context,
        CancellationToken ct)
    {
        var identityId = Uuid.CreateVersion5(IdentityNamespaceId, $"{provider}\n{identifier}");
        var identity = await repository.HydrateAsync(new UserIdentity(identityId), ct);
        if (identity.IsRegistered)
        {
            if (!string.Equals(identity.Provider, provider, StringComparison.Ordinal) ||
                !string.Equals(identity.Identifier, identifier, StringComparison.Ordinal))
            {
                return Result<RegisteredUserIdentity>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The deterministic identity is associated with different provider credentials."));
            }

            if (existingUserId is not null && identity.UserId != existingUserId.Value)
            {
                return Result<RegisteredUserIdentity>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The provider identity belongs to another user."));
            }

            return Result<RegisteredUserIdentity>.Success(new RegisteredUserIdentity(
                identity.Id,
                identity.UserId,
                identity.EmailAddress));
        }

        var userId = existingUserId ?? Uuid.CreateVersion4();
        var registration = identity.Register(userId, provider, identifier, emailAddress);
        if (!registration.IsSuccess)
        {
            return Result<RegisteredUserIdentity>.Failure(registration.Error);
        }

        await repository.SaveAsync(identity, context, ct);
        return Result<RegisteredUserIdentity>.Success(new RegisteredUserIdentity(
            identity.Id,
            userId,
            emailAddress));
    }
}

/// <summary>Registers a deterministic development identity from a normalized email address.</summary>
public sealed class RegisterDeveloperUserHandler(
    UserIdentityRegistration registration,
    DeveloperUserRegistration developerRegistration)
    : IRequestHandler<RegisterDeveloperUser, RegisteredUserIdentity>
{
    public ValueTask<Result<RegisteredUserIdentity>> HandleAsync(
        IRequestContext<RegisterDeveloperUser> context,
        CancellationToken ct)
    {
        if (!developerRegistration.Enabled)
        {
            return ValueTask.FromResult(Result<RegisteredUserIdentity>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "Developer registration is only available when developer authentication is enabled.")));
        }

        if (!EmailAddresses.TryNormalize(context.Request.EmailAddress, out var normalizedEmail))
        {
            return ValueTask.FromResult(Result<RegisteredUserIdentity>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "Enter a valid email address.")));
        }

        var identifier = Uuid.CreateVersion5(
            DeveloperUserRegistration.IdentifierNamespaceId,
            normalizedEmail).ToString();
        return registration.RegisterAsync(
            DeveloperUserRegistration.Provider,
            identifier,
            normalizedEmail,
            ExistingUserId(context.Actor),
            context,
            ct);
    }

    static Uuid? ExistingUserId(ClaimsPrincipal actor) =>
        UserIdentityRegistrationClaims.TryGetBdgrzSubject(actor, out var userId) ? userId : null;
}

/// <summary>Registers identity claims validated by the configured OpenID Connect provider.</summary>
public sealed class RegisterOidcUserHandler(UserIdentityRegistration registration)
    : IRequestHandler<RegisterOidcUser, RegisteredUserIdentity>
{
    public ValueTask<Result<RegisteredUserIdentity>> HandleAsync(
        IRequestContext<RegisterOidcUser> context,
        CancellationToken ct)
    {
        var actor = context.Actor;
        var providerIdentity = actor.Identities.FirstOrDefault(identity =>
            identity.IsAuthenticated &&
            !string.Equals(identity.FindFirst("iss")?.Value, "bdgrz", StringComparison.Ordinal));
        var provider = providerIdentity?.FindFirst("iss")?.Value;
        var identifier = providerIdentity?.FindFirst("sub")?.Value;
        if (actor.Identity?.IsAuthenticated is not true ||
            string.IsNullOrWhiteSpace(provider) ||
            string.IsNullOrWhiteSpace(identifier))
        {
            return ValueTask.FromResult(Result<RegisteredUserIdentity>.Failure(new RequestError(
                RequestErrorKind.Unauthorized,
                "An authenticated OIDC issuer and subject are required.")));
        }

        var assertedEmail = providerIdentity?.FindFirst("email")?.Value;
        var emailAddress = EmailAddresses.TryNormalize(assertedEmail, out var normalizedEmail)
            ? normalizedEmail
            : null;
        return registration.RegisterAsync(
            provider.Trim(),
            identifier.Trim(),
            emailAddress,
            UserIdentityRegistrationClaims.TryGetBdgrzSubject(actor, out var userId) ? userId : null,
            context,
            ct);
    }
}

static class UserIdentityRegistrationClaims
{
    public static bool TryGetBdgrzSubject(ClaimsPrincipal actor, out Uuid userId)
    {
        var identity = actor.Identities.FirstOrDefault(candidate =>
            string.Equals(candidate.FindFirst("iss")?.Value, "bdgrz", StringComparison.Ordinal));
        var value = identity?.FindFirst("sub")?.Value;
        return Uuid.TryParse(value, CultureInfo.InvariantCulture, out userId) && userId != Uuid.Empty;
    }
}

static class EmailAddresses
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = value?.Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant() ?? string.Empty;
        if (normalized.Length is 0 or > 254)
        {
            return false;
        }

        try
        {
            var parsed = new MailAddress(normalized);
            return string.Equals(parsed.Address, normalized, StringComparison.Ordinal) &&
                   normalized.Contains('@', StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
