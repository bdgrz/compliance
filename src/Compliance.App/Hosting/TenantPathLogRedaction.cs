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
    /// Runs after configuration binding, so an operator-supplied category or provider-specific
    /// level cannot re-enable path-bearing framework diagnostics below <see cref="LogLevel.Warning"/>.
    /// </summary>
    internal static void Apply(LoggerFilterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // A rule is added for every provider named in configuration because provider-specific
        // rules, including a provider's Default, take precedence over provider-neutral rules.
        var providers = options.Rules.Select(rule => rule.ProviderName)
            .Append(null)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        for (var index = options.Rules.Count - 1; index >= 0; index--)
        {
            var rule = options.Rules[index];
            if (IsAspNetCoreCategory(rule.CategoryName) &&
                (rule.LogLevel is null || rule.LogLevel < LogLevel.Warning))
            {
                options.Rules.RemoveAt(index);
            }
        }

        foreach (var provider in providers)
        {
            if (!options.Rules.Any(rule =>
                    string.Equals(rule.ProviderName, provider, StringComparison.Ordinal) &&
                    string.Equals(rule.CategoryName, AspNetCore, StringComparison.Ordinal)))
            {
                options.Rules.Add(new LoggerFilterRule(provider, AspNetCore, LogLevel.Warning, null));
            }

            options.Rules.Add(new LoggerFilterRule(provider, HostingDiagnostics, LogLevel.None, null));
        }
    }

    static bool IsAspNetCoreCategory(string? category) =>
        category is not null &&
        (string.Equals(category, AspNetCore, StringComparison.Ordinal) ||
            category.StartsWith(AspNetCore + ".", StringComparison.Ordinal));
}
