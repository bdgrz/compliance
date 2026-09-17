namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Normalizes the raw <c>Search</c>/<c>Sort</c> members shared by every list request. Each
///     directory has exactly one sortable index today, so <c>Sort</c> only ever toggles direction.
/// </summary>
static class ListRequestNormalization
{
    public static string? NormalizeSearch(string? search) =>
        string.IsNullOrWhiteSpace(search) ? null : search.Trim();

    public static bool IsDescending(string? sort) =>
        sort is not null && sort.Contains("desc", StringComparison.OrdinalIgnoreCase);
}
