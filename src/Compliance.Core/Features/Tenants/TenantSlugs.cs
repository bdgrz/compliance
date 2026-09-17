using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Normalizes and validates tenant slugs against the rule the backlog defines for R1-15: 4-63
///     characters, starts with a letter, ends with a letter or digit, no consecutive hyphens, not a
///     reserved top-level route, and not shaped like a <see cref="Uuid" /> (so a slug can never
///     collide with the <c>tenant_id</c> identifier format).
/// </summary>
public static class TenantSlugs
{
    // Every top-level server or client route segment. Adding a new top-level route requires adding
    // it here first, per the backlog's registry-ownership rule.
    static readonly HashSet<string> ReservedRoutes = new(StringComparer.Ordinal)
    {
        "api", "auth", "health", "healthz", "openapi", "login", "logout", "signup", "callback",
        "select", "tenants", "organizations", "new", "create", "account", "settings", "admin",
        "portfolio", "work", "my-work", "invitations", "invite", "help", "support", "docs",
        "status", "static", "assets", "www", "app",
    };

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length is < 4 or > 63 || normalized[0] is < 'a' or > 'z' || normalized[^1] == '-')
        {
            return false;
        }

        var previousHyphen = false;
        foreach (var character in normalized)
        {
            var hyphen = character == '-';
            if (!(character is >= 'a' and <= 'z' or >= '0' and <= '9') && !hyphen ||
                hyphen && previousHyphen)
            {
                return false;
            }

            previousHyphen = hyphen;
        }

        if (ReservedRoutes.Contains(normalized) || Uuid.TryParse(normalized, null, out _))
        {
            return false;
        }

        return true;
    }
}
