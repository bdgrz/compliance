namespace Bdgrz.Compliance.Features.Authentication;

/// <summary>Provider-neutral OpenID Connect settings for the Compliance API and browser client.</summary>
public sealed record ComplianceAuthenticationSettings(
    string Authority,
    string ClientId,
    string[] Scopes,
    string? AuthorizationAudience,
    ComplianceAuthenticationResource[] Resources)
{
    const string ConfigurationSection = "Compliance:Authentication";

    public static ComplianceAuthenticationSettings? Resolve(
        IConfiguration configuration,
        bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(ConfigurationSection);
        var developerAuthentication = configuration.GetValue("BDGRZ_DEVELOPER_AUTH", false);
        var configuredMode = developerAuthentication
            ? nameof(ComplianceAuthenticationMode.Development)
            : section["Mode"] ?? nameof(ComplianceAuthenticationMode.External);

        if (!Enum.TryParse<ComplianceAuthenticationMode>(configuredMode, ignoreCase: true, out var mode))
        {
            throw new InvalidOperationException(
                $"Compliance authentication Mode must be '{nameof(ComplianceAuthenticationMode.External)}' " +
                $"or '{nameof(ComplianceAuthenticationMode.Development)}'.");
        }

        if (mode is ComplianceAuthenticationMode.Development)
        {
            if (!isDevelopment)
            {
                throw new InvalidOperationException(
                    "The Compliance development authentication bypass is only available in the Development environment.");
            }

            return null;
        }

        var authority = Required(section, "Authority");
        var clientId = Required(section, "ClientId");
        var requireHttpsMetadata = section.GetValue("RequireHttpsMetadata", defaultValue: true);

        if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri) ||
            (requireHttpsMetadata && authorityUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "Compliance authentication Authority must be an absolute HTTPS URI.");
        }

        var scopes = (section["Scopes"] ?? "openid profile")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!scopes.Contains("openid", StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Compliance authentication Scopes must include 'openid'.");
        }

        var resources = ResolveResources(section, authority, requireHttpsMetadata);

        return new ComplianceAuthenticationSettings(
            authority,
            clientId,
            scopes,
            NullIfWhiteSpace(section["AuthorizationAudience"]),
            resources);
    }

    internal ComplianceAuthenticationClientConfiguration ToClientConfiguration() =>
        new(true, false, Authority, ClientId, Scopes, AuthorizationAudience);

    static string Required(IConfigurationSection section, string key) =>
        NullIfWhiteSpace(section[key]) ?? throw new InvalidOperationException(
            $"Compliance authentication requires {key} when Mode is External.");

    static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    static ComplianceAuthenticationResource[] ResolveResources(
        IConfigurationSection authentication,
        string defaultAuthority,
        bool defaultRequireHttpsMetadata)
    {
        var configured = authentication.GetSection("Resources").GetChildren().ToArray();
        if (configured.Length == 0)
        {
            return
            [
                new ComplianceAuthenticationResource(
                    "Default",
                    defaultAuthority,
                    Required(authentication, "Audience"),
                    defaultRequireHttpsMetadata),
            ];
        }

        var resources = new ComplianceAuthenticationResource[configured.Length];
        for (var index = 0; index < configured.Length; index++)
        {
            var resource = configured[index];
            var authority = NullIfWhiteSpace(resource["Authority"]) ?? defaultAuthority;
            var requireHttpsMetadata = resource.GetValue(
                "RequireHttpsMetadata",
                defaultRequireHttpsMetadata);
            if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri) ||
                (requireHttpsMetadata && authorityUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    $"Compliance authentication resource '{resource.Key}' Authority must be an absolute HTTPS URI.");
            }

            resources[index] = new ComplianceAuthenticationResource(
                resource.Key,
                authority,
                Required(resource, "Audience"),
                requireHttpsMetadata);
        }

        return resources;
    }
}
