namespace Bdgrz.Compliance.Features.Tenants;

public static class TenantSlugs
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length is < 3 or > 63 || normalized[0] == '-' || normalized[^1] == '-')
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

        return true;
    }
}
