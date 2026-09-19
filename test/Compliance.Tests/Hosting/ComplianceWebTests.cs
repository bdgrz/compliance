using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.Hosting;

public sealed class ComplianceWebTests
{
    [Fact]
    public async Task EveryBusinessApiEndpointDeclaresAuthorization()
    {
        await using var factory = CreateBrokerFreeFactory("Development");
        using var client = factory.CreateClient();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/v1/", StringComparison.Ordinal) == true)
            .Where(endpoint => endpoint.RoutePattern.RawText is not
                "/api/v1/developer-user-sessions" and not "/api/v1/oidc-user-sessions")
            .ToArray();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, endpoint => Assert.NotEmpty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()));
    }

    [Fact]
    public async Task ShouldRegisterBearerSchemeForEachConfiguredResource()
    {
        // Arrange
        await using var factory = CreateBrokerFreeFactory("Production").WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Compliance:Authentication:Resources:Badgers:Audience", "badgers-api");
            builder.UseSetting(
                "Compliance:Authentication:Resources:Evidence:Authority",
                "https://evidence.example/");
            builder.UseSetting("Compliance:Authentication:Resources:Evidence:Audience", "evidence-api");
        });

        // Act
        var schemes = await factory.Services
            .GetRequiredService<IAuthenticationSchemeProvider>()
            .GetAllSchemesAsync();

        // Assert
        Assert.Contains(schemes, scheme => scheme.Name == "BdgrzResource0");
        Assert.Contains(schemes, scheme => scheme.Name == "BdgrzResource1");
        Assert.Contains(schemes, scheme => scheme.Name == "BdgrzSession");
    }

    [Fact]
    public async Task ShouldChallengeUnknownApiRouteInProduction()
    {
        // Arrange
        await using var factory = CreateBrokerFreeFactory("Production");
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/missing", CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, challenge => challenge.Scheme == "Bearer");
    }

    [Fact]
    public async Task ShouldChallengeUnknownApiRouteInDevelopmentWithoutSession()
    {
        // Arrange
        await using var factory = CreateBrokerFreeFactory("Development");
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/missing", CancellationToken.None);
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ShouldServeSpaFallbackOutsideApiBoundary()
    {
        // Arrange
        await using var factory = CreateBrokerFreeFactory("Development");
        using var client = factory.CreateClient();

        // Act
        using var index = await client.GetAsync("/index.html", CancellationToken.None);
        using var response = await client.GetAsync("/controls/example", CancellationToken.None);
        using var callback = await client.GetAsync("/auth/callback", CancellationToken.None);
        using var registration = await client.GetAsync("/developer-login?returnUrl=%2Fcontrols", CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, callback.StatusCode);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        Assert.Contains("<div id=\"app\"></div>", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldExposeOnlyPublicExternalAuthenticationConfiguration()
    {
        // Arrange
        await using var factory = CreateBrokerFreeFactory("Production");
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/auth/config", CancellationToken.None);
        var body = await response.Content.ReadFromJsonAsync<AuthenticationConfigurationDocument>(
            CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.True(body.Enabled);
        Assert.False(body.DeveloperIdentityEnabled);
        Assert.Equal("https://issuer.example/", body.Issuer);
        Assert.Equal("compliance-spa", body.ClientId);
        Assert.Equal(["openid", "profile"], body.Scopes);
    }

    [Fact]
    public async Task ShouldExposeLivenessReadinessAndOpenApiInDevelopment()
    {
        // Arrange
        await using var factory = CreateBrokerFreeFactory("Development");
        using var client = factory.CreateClient();

        // Act
        using var liveness = await client.GetAsync("/health/live", CancellationToken.None);
        using var readiness = await client.GetAsync("/health/ready", CancellationToken.None);
        using var openApi = await client.GetAsync("/openapi/v1.json", CancellationToken.None);
        var openApiDocument = await openApi.Content.ReadAsStringAsync(CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, liveness.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);
        Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);
        Assert.Contains("/api/v1/developer-user-sessions", openApiDocument, StringComparison.Ordinal);
    }

    static WebApplicationFactory<Program> CreateBrokerFreeFactory(string environment) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting(
                "Compliance:Authentication:Mode",
                environment == "Development" ? "Development" : "External");
            builder.UseSetting("Compliance:Authentication:Authority", "https://issuer.example/");
            builder.UseSetting("Compliance:Authentication:Audience", "compliance-api");
            builder.UseSetting("Compliance:Authentication:ClientId", "compliance-spa");
            builder.UseSetting("Compliance:Authentication:Scopes", "openid profile");
            builder.UseSetting("BDGRZ_SESSION_SIGNING_KEY", "bdgrz-test-session-signing-key-000001");
            builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
            builder.UseSetting("Fitz:ApplicationName", "compliance-tests");
            builder.ConfigureServices(services =>
            {
                var brokerHostedServices = services
                    .Where(descriptor =>
                        descriptor.ServiceType == typeof(IHostedService) &&
                        descriptor.ImplementationType?.Name != "ComplianceReadinessLifecycle")
                    .ToArray();

                foreach (var descriptor in brokerHostedServices)
                {
                    services.Remove(descriptor);
                }
            });
        });

    sealed record AuthenticationConfigurationDocument(
        bool Enabled,
        [property: JsonPropertyName("developer_identity_enabled")]
        bool DeveloperIdentityEnabled,
        string? Issuer,
        [property: JsonPropertyName("client_id")] string? ClientId,
        string[] Scopes);
}
