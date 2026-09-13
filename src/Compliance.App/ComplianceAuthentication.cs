using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Bdgrz.Compliance;

enum ComplianceAuthenticationMode
{
    External,
    Development,
}

/// <summary>
/// Provider-neutral OpenID Connect settings for the Compliance API and browser client.
/// </summary>
public sealed record ComplianceAuthenticationSettings(
    string Authority,
    string Audience,
    string ClientId,
    string[] Scopes,
    string? AuthorizationAudience,
    bool RequireHttpsMetadata)
{
    const string ConfigurationSection = "Compliance:Authentication";

    /// <summary>
    /// Resolves authentication settings, failing closed unless an explicit development bypass is allowed.
    /// </summary>
    public static ComplianceAuthenticationSettings? Resolve(
        IConfiguration configuration,
        bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(ConfigurationSection);
        var configuredMode = section["Mode"] ?? nameof(ComplianceAuthenticationMode.External);

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
        var audience = Required(section, "Audience");
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

        return new ComplianceAuthenticationSettings(
            authority,
            audience,
            clientId,
            scopes,
            NullIfWhiteSpace(section["AuthorizationAudience"]),
            requireHttpsMetadata);
    }

    internal ComplianceAuthenticationClientConfiguration ToClientConfiguration() =>
        new(
            Enabled: true,
            Issuer: Authority,
            ClientId,
            Scopes,
            AuthorizationAudience);

    static string Required(IConfigurationSection section, string key) =>
        NullIfWhiteSpace(section[key]) ?? throw new InvalidOperationException(
            $"Compliance authentication requires {key} when Mode is External.");

    static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

sealed record ComplianceAuthenticationClientConfiguration(
    bool Enabled,
    string? Issuer,
    string? ClientId,
    string[] Scopes,
    string? AuthorizationAudience)
{
    internal static ComplianceAuthenticationClientConfiguration Development { get; } =
        new(false, null, null, [], null);
}

/// <summary>
/// Registers provider-neutral JWT access-token validation for the Compliance API.
/// </summary>
public static class ComplianceAuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Configures JWT bearer validation against an external OpenID Connect authority.
    /// </summary>
    public static ComplianceAuthenticationSettings? AddComplianceAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var settings = ComplianceAuthenticationSettings.Resolve(
            configuration,
            environment.IsDevelopment());

        if (settings is null)
        {
            services.AddAuthentication();
            services.AddAuthorization();
            return null;
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = settings.Authority;
                options.Audience = settings.Audience;
                options.RequireHttpsMetadata = settings.RequireHttpsMetadata;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidateIssuer = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                };
            });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();
        });

        return settings;
    }
}
