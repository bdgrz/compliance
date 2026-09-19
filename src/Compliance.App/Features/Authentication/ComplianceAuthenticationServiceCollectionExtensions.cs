using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Bdgrz.Compliance.Features.Authentication;

/// <summary>Registers provider-neutral JWT access-token validation for the Compliance API.</summary>
public static class ComplianceAuthenticationServiceCollectionExtensions
{
    public static ComplianceAuthenticationSettings? AddComplianceAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var settings = ComplianceAuthenticationSettings.Resolve(configuration, environment.IsDevelopment());
        var sessionTokens = BdgrzSessionTokens.Resolve(configuration, environment.IsDevelopment());
        services.AddSingleton(sessionTokens);

        if (settings is null)
        {
            services
                .AddAuthentication(ComplianceAuthenticationSchemes.Session)
                .AddBdgrzSession(sessionTokens);
            services.AddAuthorization(options =>
            {
                options.AddPolicy(
                    ComplianceAuthorizationPolicies.ApiUser,
                    policy => policy
                        .AddAuthenticationSchemes(ComplianceAuthenticationSchemes.Session)
                        .RequireAuthenticatedUser());
                options.AddPolicy(
                    ComplianceAuthorizationPolicies.Session,
                    policy => policy
                        .AddAuthenticationSchemes(ComplianceAuthenticationSchemes.Session)
                        .RequireAuthenticatedUser());
            });
            return null;
        }

        var authentication = services.AddAuthentication();
        var resourceSchemes = new string[settings.Resources.Length];
        for (var index = 0; index < settings.Resources.Length; index++)
        {
            var resource = settings.Resources[index];
            var scheme = ResourceScheme(index);
            resourceSchemes[index] = scheme;
            authentication.AddJwtBearer(scheme, options =>
            {
                options.Authority = resource.Authority;
                options.Audience = resource.Audience;
                options.RequireHttpsMetadata = resource.RequireHttpsMetadata;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidateIssuer = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                };
            });
        }

        authentication.AddBdgrzSession(sessionTokens);

        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder(
                    [.. resourceSchemes, ComplianceAuthenticationSchemes.Session])
                .RequireAuthenticatedUser()
                .Build();
            options.AddPolicy(
                ComplianceAuthorizationPolicies.ApiUser,
                policy => policy
                    .AddAuthenticationSchemes(
                        [.. resourceSchemes, ComplianceAuthenticationSchemes.Session])
                    .RequireAuthenticatedUser());
            options.AddPolicy(
                ComplianceAuthorizationPolicies.Session,
                policy => policy
                    .AddAuthenticationSchemes(ComplianceAuthenticationSchemes.Session)
                    .RequireAuthenticatedUser());
            options.AddPolicy(
                ComplianceAuthorizationPolicies.OidcContinuation,
                policy => policy
                    .AddAuthenticationSchemes(
                        [.. resourceSchemes, ComplianceAuthenticationSchemes.Session])
                    .RequireAuthenticatedUser());
            options.AddPolicy(
                ComplianceAuthorizationPolicies.IdentityLink,
                policy => policy
                    .AddAuthenticationSchemes(
                        [.. resourceSchemes, ComplianceAuthenticationSchemes.Session])
                    .RequireAssertion(context =>
                        context.User.Identities.Any(identity => identity.IsAuthenticated &&
                            string.Equals(identity.FindFirst("iss")?.Value, "bdgrz",
                                StringComparison.Ordinal)) &&
                        context.User.Identities.Any(identity => identity.IsAuthenticated &&
                            !string.Equals(identity.FindFirst("iss")?.Value, "bdgrz",
                                StringComparison.Ordinal) &&
                            !string.IsNullOrWhiteSpace(identity.FindFirst("iss")?.Value) &&
                            !string.IsNullOrWhiteSpace(identity.FindFirst("sub")?.Value))));
        });

        return settings;
    }

    static string ResourceScheme(int index) => $"BdgrzResource{index}";

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
