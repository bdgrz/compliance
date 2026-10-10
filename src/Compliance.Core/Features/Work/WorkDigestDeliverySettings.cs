using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Features.Work;

public sealed record WorkDigestDeliverySettings(string? ApplicationOrigin,
    string? ClientWorkItemRouteTemplate, int MaximumAttempts, TimeSpan RetryDelay,
    TimeSpan RetryWindow, TimeSpan AttemptLease, TimeSpan PollInterval)
{
    public bool IsReady => HasCanonicalOrigin(ApplicationOrigin) &&
                           HasScopedClientRoute(ClientWorkItemRouteTemplate);

    public static WorkDigestDeliverySettings FromConfiguration(IConfiguration configuration,
        bool requireConfiguredLinks)
    {
        var section = configuration.GetSection("Compliance:WorkDigest");
        var settings = new WorkDigestDeliverySettings(
            section["ApplicationOrigin"]?.Trim(),
            section["ClientWorkItemRouteTemplate"]?.Trim(),
            ReadInt(section["MaximumAttempts"], 3, 1, 10, "maximum attempts"),
            TimeSpan.FromSeconds(ReadInt(section["RetryDelaySeconds"], 300, 1, 86400,
                "retry delay")),
            TimeSpan.FromHours(ReadInt(section["RetryWindowHours"], 144, 1, 167,
                "retry window")),
            TimeSpan.FromSeconds(ReadInt(section["AttemptLeaseSeconds"], 180, 30, 3600,
                "attempt lease")),
            TimeSpan.FromSeconds(ReadInt(section["PollIntervalSeconds"], 60, 5, 3600,
                "poll interval")));
        if (requireConfiguredLinks && !settings.IsReady)
            throw new InvalidOperationException(
                "Weekly digest delivery requires an HTTPS application origin and scoped client route.");
        return settings;
    }

    public Uri? CreateWorkItemUri(Cntryl.Portia.Uuid tenantId, string tenantSlug,
        Cntryl.Portia.Uuid programId, Cntryl.Portia.Uuid workItemId)
    {
        if (!IsReady || !IsSafePathSegment(tenantSlug))
            return null;
        var path = ClientWorkItemRouteTemplate!
            .Replace("{tenant_id}", Uri.EscapeDataString(tenantId.ToString()),
                StringComparison.Ordinal)
            .Replace("{tenant_slug}", Uri.EscapeDataString(tenantSlug), StringComparison.Ordinal)
            .Replace("{program_id}", Uri.EscapeDataString(programId.ToString()),
                StringComparison.Ordinal)
            .Replace("{work_item_id}", Uri.EscapeDataString(workItemId.ToString()),
                StringComparison.Ordinal);
        if (!Uri.TryCreate(new Uri(ApplicationOrigin!, UriKind.Absolute), path, out var link) ||
            link.Scheme != Uri.UriSchemeHttps)
            return null;
        var pathSegments = link.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString).ToArray();
        var scopeSegments = new[]
        {
            tenantId.ToString(), tenantSlug, programId.ToString(), workItemId.ToString(),
        };
        return scopeSegments.All(expected => pathSegments.Count(segment =>
            string.Equals(segment, expected, StringComparison.Ordinal)) == 1) ? link : null;
    }

    static int ReadInt(string? value, int fallback, int minimum, int maximum, string description)
    {
        if (value is null)
            return fallback;
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < minimum || parsed > maximum)
            throw new InvalidOperationException($"Weekly digest {description} is invalid.");
        return parsed;
    }

    static bool HasCanonicalOrigin(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var origin) &&
        origin.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(origin.UserInfo) &&
        origin.AbsolutePath == "/" && string.IsNullOrEmpty(origin.Query) &&
        string.IsNullOrEmpty(origin.Fragment);

    static bool HasScopedClientRoute(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith('/') ||
            value.StartsWith("//", StringComparison.Ordinal) || value.Contains('?') ||
            value.Contains('#') || value.Contains('\\'))
            return false;

        var segments = value[1..].Split('/');
        if (segments.Any(static segment => segment.Length == 0 || IsDotSegment(segment)))
            return false;
        var requiredSegments = new[]
        {
            "{tenant_id}", "{tenant_slug}", "{program_id}", "{work_item_id}",
        };
        return requiredSegments.All(required =>
            segments.Count(segment => string.Equals(segment, required,
                StringComparison.Ordinal)) == 1);
    }

    static bool IsDotSegment(string segment)
    {
        var decoded = segment;
        for (var pass = 0; pass <= segment.Length; pass++)
        {
            if (decoded is "." or ".." || decoded.Contains('/') || decoded.Contains('\\'))
                return true;
            var next = Uri.UnescapeDataString(decoded);
            if (string.Equals(next, decoded, StringComparison.Ordinal))
                return false;
            decoded = next;
        }
        return true;
    }

    static bool IsSafePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var decoded = value;
        for (var pass = 0; pass <= value.Length; pass++)
        {
            if (decoded is "." or ".." || decoded.Contains('/') || decoded.Contains('\\') ||
                decoded.Any(char.IsControl))
                return false;
            var next = Uri.UnescapeDataString(decoded);
            if (string.Equals(next, decoded, StringComparison.Ordinal))
                return true;
            decoded = next;
        }
        return false;
    }
}
