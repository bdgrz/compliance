using System.Security.Claims;

namespace Bdgrz.Compliance.Features.UserIdentities;

static class OidcProviderClaims
{
    public static bool TryGet(ClaimsPrincipal actor, out string provider,
        out string identifier, out string? emailAddress)
    {
        var identity = actor.Identities.FirstOrDefault(candidate =>
            candidate.IsAuthenticated &&
            !string.Equals(candidate.FindFirst("iss")?.Value, "bdgrz", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(candidate.FindFirst("iss")?.Value) &&
            !string.IsNullOrWhiteSpace(candidate.FindFirst("sub")?.Value));
        provider = identity?.FindFirst("iss")?.Value?.Trim() ?? string.Empty;
        identifier = identity?.FindFirst("sub")?.Value?.Trim() ?? string.Empty;
        var assertedEmail = identity?.FindFirst("email")?.Value;
        emailAddress = EmailAddresses.TryNormalize(assertedEmail, out var normalizedEmail)
            ? normalizedEmail
            : null;
        return provider.Length > 0 && identifier.Length > 0;
    }
}
