using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task ShouldDeclareAuthorizationGivenBusinessApiEndpoint(string environment)
    {
        // Arrange
        await using var factory = CreateBrokerFreeFactory(environment);
        using var client = factory.CreateClient();

        // Act
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/v1/", StringComparison.Ordinal) == true)
            .Where(endpoint => endpoint.RoutePattern.RawText is not
                "/api/v1/developer-user-sessions" and not "/api/v1/oidc-user-sessions")
            .ToArray();

        // Assert
        Assert.NotEmpty(endpoints);
        var policies = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        foreach (var endpoint in endpoints)
        {
            var authorizations = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
            Assert.NotEmpty(authorizations);
            var policyName = Assert.IsType<string>(Assert.Single(authorizations).Policy);
            Assert.Equal("BdgrzApiUser", policyName);
            Assert.NotNull(await policies.GetPolicyAsync(policyName));
            Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
        }
    }

    [Fact]
    public async Task ShouldRegisterBearerSchemeGivenConfiguredResource()
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
    public async Task ShouldChallengeUnknownRouteGivenProductionMode()
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
    public async Task ShouldChallengeUnknownRouteGivenDevelopmentWithoutSession()
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
    public async Task ShouldServeSpaFallbackGivenNonApiRoute()
    {
        // Arrange
        await using var factory = CreateBrokerFreeFactory("Development");
        using var client = factory.CreateClient();

        // Act
        using var index = await client.GetAsync("/index.html", CancellationToken.None);
        using var response = await client.GetAsync("/client-name/controls/example", CancellationToken.None);
        using var callback = await client.GetAsync("/auth/callback", CancellationToken.None);
        using var registration = await client.GetAsync("/developer-login?returnUrl=%2Fcontrols", CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, callback.StatusCode);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        Assert.Contains("<div id=\"app\"></div>", body, StringComparison.Ordinal);
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Equal("no-referrer", Assert.Single(index.Headers.GetValues("Referrer-Policy")));
    }

    [Fact]
    public async Task ShouldExposePublicAuthSettingsGivenExternalConfiguration()
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
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.NotNull(body);
        Assert.True(body.Enabled);
        Assert.False(body.DeveloperIdentityEnabled);
        Assert.Equal("https://issuer.example/", body.Issuer);
        Assert.Equal("compliance-spa", body.ClientId);
        Assert.Equal(["openid", "profile"], body.Scopes);
    }

    [Fact]
    public async Task ShouldExposeHealthAndOpenApiGivenDevelopmentMode()
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

    [Fact]
    public async Task ShouldDescribeServiceAndBoundaryContractsGivenOpenApi()
    {
        // Arrange
        await using var factory = CreateBrokerFreeFactory("Development");
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/openapi/v1.json", CancellationToken.None);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(
            CancellationToken.None));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/v1/tenants/{tenant_id}/client-services")
            .GetProperty("post").TryGetProperty("requestBody", out _));
        Assert.True(paths.GetProperty("/api/v1/tenants/{tenant_id}/client-services/{service_id}")
            .GetProperty("get").GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(paths.GetProperty("/api/v1/tenants/{tenant_id}/programs/{program_id}/boundaries")
            .GetProperty("post").TryGetProperty("requestBody", out _));
        Assert.True(paths.GetProperty("/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/reviews")
            .GetProperty("post").TryGetProperty("requestBody", out _));
        Assert.True(paths.GetProperty("/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/approvals")
            .GetProperty("post").TryGetProperty("requestBody", out _));
    }

    [Fact]
    public async Task ShouldUseSnakeCaseGivenApiRouteQueryAndJsonNames()
    {
        // Arrange
        await using var factory = CreateBrokerFreeFactory("Development");
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/openapi/v1.json", CancellationToken.None);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(
            CancellationToken.None));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paths = document.RootElement.GetProperty("paths");
        foreach (var path in paths.EnumerateObject())
        {
            foreach (Match token in Regex.Matches(path.Name, @"\{([^}]+)\}"))
                Assert.Matches("^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$", token.Groups[1].Value);
            foreach (var operation in path.Value.EnumerateObject())
            {
                if (!operation.Value.TryGetProperty("parameters", out var parameters))
                    continue;
                foreach (var parameter in parameters.EnumerateArray())
                    Assert.Matches("^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$",
                        parameter.GetProperty("name").GetString()!);
            }
        }

        AssertSnakeCaseJsonProperties(document.RootElement);
    }

    static void AssertSnakeCaseJsonProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                AssertSnakeCaseJsonProperties(child);
            return;
        }
        if (element.ValueKind != JsonValueKind.Object)
            return;
        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals("properties"))
                foreach (var jsonProperty in property.Value.EnumerateObject())
                    Assert.Matches("^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$", jsonProperty.Name);
            AssertSnakeCaseJsonProperties(property.Value);
        }
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
