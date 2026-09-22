namespace Bdgrz.Compliance.Hosting;

/// <summary>
/// Keeps request paths, which can carry a client-identifying organization slug, out of logs and
/// log scopes. Telemetry identifies an organization only by its opaque tenant ID (ADR 0009).
/// </summary>
static class TenantPathLogRedaction
{
    const string AspNetCore = "Microsoft.AspNetCore";

    // Request start/finish events and the RequestPath scope. The scope exists whenever this
    // category is enabled at any level, so the category is disabled outright.
    const string HostingDiagnostics = "Microsoft.AspNetCore.Hosting.Diagnostics";

    public static IServiceCollection AddTenantPathLogRedaction(this IServiceCollection services) =>
        services.PostConfigure<LoggerFilterOptions>(Apply);

    /// <summary>
    /// Runs after configuration binding. Every configured rule that could select a
    /// <c>Microsoft.AspNetCore</c> category ahead of the rules added here is removed, so neither a
    /// category, a differently cased, a wildcard, nor a provider-specific override can re-enable
    /// path-bearing framework diagnostics below <see cref="LogLevel.Warning"/>.
    /// </summary>
    internal static void Apply(LoggerFilterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // A rule is added for every provider named in configuration because provider-specific
        // rules, including a provider's Default, take precedence over provider-neutral rules.
        var providers = options.Rules.Select(rule => rule.ProviderName)
            .Append(null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // The logger selects the matching rule with the longest category name, and the last one
        // on a tie. A shorter matching rule already loses to the rules appended below.
        for (var index = options.Rules.Count - 1; index >= 0; index--)
        {
            if (CouldOutrankRedaction(options.Rules[index].CategoryName))
                options.Rules.RemoveAt(index);
        }

        foreach (var provider in providers)
        {
            options.Rules.Add(new LoggerFilterRule(provider, AspNetCore, LogLevel.Warning, null));
            options.Rules.Add(new LoggerFilterRule(provider, HostingDiagnostics, LogLevel.None, null));
        }
    }

    static bool CouldOutrankRedaction(string? category)
    {
        if (category is null || category.Length < AspNetCore.Length)
            return false;

        // Category rules match case-insensitively and may contain one '*' wildcard.
        var wildcard = category.IndexOf('*', StringComparison.Ordinal);
        var prefix = wildcard < 0 ? category : category[..wildcard];
        return prefix.StartsWith(AspNetCore, StringComparison.OrdinalIgnoreCase) ||
            AspNetCore.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
