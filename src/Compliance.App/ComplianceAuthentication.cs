using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
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
            DeveloperRegistrationEnabled: false,
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
    bool DeveloperRegistrationEnabled,
    string? Issuer,
    string? ClientId,
    string[] Scopes,
    string? AuthorizationAudience)
{
    internal static ComplianceAuthenticationClientConfiguration Development { get; } =
        new(false, true, null, null, [], null);
}

static class ComplianceAuthenticationSchemes
{
    public const string Session = "BdgrzSession";
}

static class ComplianceAuthorizationPolicies
{
    public const string Session = "BdgrzSession";
    public const string OidcRegistration = "BdgrzOidcRegistration";
}

/// <summary>Issues and validates the Bdgrz browser session JWT.</summary>
public sealed class BdgrzSessionTokens
{
    internal const string CookieName = "bdgrz_session";
    internal const string Issuer = "bdgrz";
    internal const string Audience = "bdgrz-browser";
    const string DevelopmentSigningSecret = "bdgrz-development-authentication-key-v1-only";

    readonly SymmetricSecurityKey _signingKey;

    BdgrzSessionTokens(string signingSecret)
    {
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingSecret));
    }

    internal SecurityKey SigningKey => _signingKey;

    internal static BdgrzSessionTokens Resolve(IConfiguration configuration, bool isDevelopment)
    {
        var signingSecret = configuration["BDGRZ_SESSION_SIGNING_KEY"];
        if (string.IsNullOrWhiteSpace(signingSecret))
        {
            if (!isDevelopment)
            {
                throw new InvalidOperationException(
                    "BDGRZ_SESSION_SIGNING_KEY is required outside the Development environment.");
            }

            signingSecret = DevelopmentSigningSecret;
        }

        if (Encoding.UTF8.GetByteCount(signingSecret) < 32)
        {
            throw new InvalidOperationException("BDGRZ_SESSION_SIGNING_KEY must contain at least 32 bytes.");
        }

        return new BdgrzSessionTokens(signingSecret);
    }

    public string Issue(RegisteredUserIdentity user, DateTimeOffset now)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new("email_verified", "false", ClaimValueTypes.Boolean),
        };
        if (user.EmailAddress is not null)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.EmailAddress));
        }
        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            Issuer,
            Audience,
            claims,
            now.UtcDateTime,
            now.AddHours(12).UtcDateTime,
            credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
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
        var sessionTokens = BdgrzSessionTokens.Resolve(configuration, environment.IsDevelopment());
        services.AddSingleton(sessionTokens);

        if (settings is null)
        {
            services
                .AddAuthentication(ComplianceAuthenticationSchemes.Session)
                .AddBdgrzSession(sessionTokens);
            services.AddAuthorization(options => options.AddPolicy(
                ComplianceAuthorizationPolicies.Session,
                policy => policy
                    .AddAuthenticationSchemes(ComplianceAuthenticationSchemes.Session)
                    .RequireAuthenticatedUser()));
            return null;
        }

        _ = services
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
            })
            .AddBdgrzSession(sessionTokens);

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                ComplianceAuthorizationPolicies.Session,
                policy => policy
                    .AddAuthenticationSchemes(ComplianceAuthenticationSchemes.Session)
                    .RequireAuthenticatedUser());
            options.AddPolicy(
                ComplianceAuthorizationPolicies.OidcRegistration,
                policy => policy
                    .AddAuthenticationSchemes(
                        JwtBearerDefaults.AuthenticationScheme,
                        ComplianceAuthenticationSchemes.Session)
                    .RequireAuthenticatedUser());
        });

        return settings;
    }

    static AuthenticationBuilder AddBdgrzSession(
        this AuthenticationBuilder authentication,
        BdgrzSessionTokens sessionTokens) =>
        authentication.AddJwtBearer(ComplianceAuthenticationSchemes.Session, options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = BdgrzSessionTokens.Issuer,
                ValidAudience = BdgrzSessionTokens.Audience,
                IssuerSigningKey = sessionTokens.SigningKey,
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                NameClaimType = JwtRegisteredClaimNames.Email,
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (context.Request.Cookies.TryGetValue(BdgrzSessionTokens.CookieName, out var token))
                    {
                        context.Token = token;
                    }

                    return Task.CompletedTask;
                },
            };
        });
}
