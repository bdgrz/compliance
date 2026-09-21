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
                "/api/v1/developer-user-sessions" and not "/api/v1/oidc-user-sessions" and not
                "/api/v1/my/oidc_identity_links")
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
    public async Task ShouldRequireDualProofPolicyGivenIdentityLinkEndpoint()
    {
        // Arrange
        await using var factory = CreateBrokerFreeFactory("Production");
        using var client = factory.CreateClient();

        // Act
        var endpoint = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(item => item.RoutePattern.RawText == "/api/v1/my/oidc_identity_links");
        var policy = Assert.Single(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()).Policy;
        using var response = await client.PostAsync("/api/v1/my/oidc_identity_links",
            null, CancellationToken.None);

        // Assert
        Assert.Equal("BdgrzIdentityLink", policy);
        Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
        Assert.NotNull(await factory.Services.GetRequiredService<IAuthorizationPolicyProvider>()
            .GetPolicyAsync(policy!));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
        Assert.True(paths.GetProperty("/api/v1/tenants/{tenant_id}/programs/{program_id}/client-services")
            .GetProperty("post").TryGetProperty("requestBody", out _));
        Assert.True(paths.GetProperty("/api/v1/tenants/{tenant_id}/programs/{program_id}/setup-work")
            .GetProperty("get").GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(paths.GetProperty("/api/v1/tenants/{tenant_id}/client-services/{service_id}")
            .GetProperty("get").GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(paths.GetProperty(
                "/api/v1/tenants/{tenant_id}/programs/{program_id}/revisions/{revision}")
            .GetProperty("get").GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(paths.GetProperty(
                "/api/v1/tenants/{tenant_id}/client_services/{service_id}/revisions/{revision}")
            .GetProperty("get").GetProperty("responses").TryGetProperty("200", out _));
        Assert.Contains(paths.GetProperty(
                "/api/v1/tenants/{tenant_id}/programs/{program_id}/revisions")
                .GetProperty("get").GetProperty("parameters").EnumerateArray(),
            parameter => parameter.GetProperty("name").GetString() ==
                         "minimum_program_revision");
        Assert.Contains(paths.GetProperty(
                "/api/v1/tenants/{tenant_id}/client-services/{service_id}/revisions")
                .GetProperty("get").GetProperty("parameters").EnumerateArray(),
            parameter => parameter.GetProperty("name").GetString() ==
                         "minimum_service_revision");
        Assert.True(paths.GetProperty("/api/v1/tenants/{tenant_id}/programs/{program_id}/boundaries")
            .GetProperty("post").TryGetProperty("requestBody", out _));
        Assert.True(paths.GetProperty("/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/reviews")
            .GetProperty("post").TryGetProperty("requestBody", out _));
        Assert.True(paths.GetProperty("/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/approvals")
            .GetProperty("post").TryGetProperty("requestBody", out _));
        foreach (var route in new[]
                 {
                     "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/versions/{version_id}",
                     "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/versions",
                     "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/effective_version",
                 })
            Assert.Contains(paths.GetProperty(route).GetProperty("get")
                    .GetProperty("parameters").EnumerateArray(),
                parameter => parameter.GetProperty("name").GetString() ==
                             "minimum_boundary_revision");
        Assert.True(paths.GetProperty(
                "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/impact_preview")
            .GetProperty("get").GetProperty("responses").TryGetProperty("200", out _));
        Assert.False(paths.TryGetProperty(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/effective-version", out _));
        Assert.False(paths.TryGetProperty(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/impact-preview",
            out _));
    }

    [Fact]
    public async Task ShouldDescribeScopeSnapshotContractsGivenOpenApi()
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
        var freeze = paths.GetProperty("/api/v1/tenants/{tenant_id}/scope_snapshots")
            .GetProperty("post");
        var freezeSchema = freeze.GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema");
        foreach (var name in new[]
                 {
                     "program_id", "expected_program_revision", "boundary_id",
                     "approved_boundary_version_id",
                 })
        {
            Assert.True(freezeSchema.GetProperty("properties").TryGetProperty(name, out _));
            Assert.Contains(freezeSchema.GetProperty("required").EnumerateArray(),
                required => required.GetString() == name);
        }
        Assert.True(freeze.GetProperty("responses").TryGetProperty("409", out _));

        var amendment = paths.GetProperty(
                "/api/v1/tenants/{tenant_id}/scope_snapshots/{snapshot_id}/amendments")
            .GetProperty("post");
        var amendmentSchema = amendment.GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema");
        Assert.True(amendmentSchema.GetProperty("properties").TryGetProperty("reason", out _));
        Assert.Contains(amendmentSchema.GetProperty("required").EnumerateArray(),
            required => required.GetString() == "reason");
        Assert.True(amendment.GetProperty("responses").TryGetProperty("200", out _));

        var get = paths.GetProperty("/api/v1/tenants/{tenant_id}/scope_snapshots/{snapshot_id}")
            .GetProperty("get");
        Assert.Contains(get.GetProperty("parameters").EnumerateArray(), parameter =>
            parameter.GetProperty("name").GetString() == "minimum_revision" &&
            parameter.GetProperty("in").GetString() == "query");
        Assert.True(get.GetProperty("responses").TryGetProperty("200", out _));

        var verification = paths.GetProperty(
                "/api/v1/tenants/{tenant_id}/scope_snapshots/{snapshot_id}/verification")
            .GetProperty("get");
        Assert.True(verification.GetProperty("responses").TryGetProperty("200", out _));
        Assert.Contains(verification.GetProperty("parameters").EnumerateArray(), parameter =>
            parameter.GetProperty("name").GetString() == "snapshot_id" &&
            parameter.GetProperty("in").GetString() == "path");

        var list = paths.GetProperty(
                "/api/v1/tenants/{tenant_id}/programs/{program_id}/scope_snapshots")
            .GetProperty("get");
        Assert.Contains(list.GetProperty("parameters").EnumerateArray(), parameter =>
            parameter.GetProperty("name").GetString() == "cursor" &&
            parameter.GetProperty("in").GetString() == "query");
        Assert.True(list.GetProperty("responses").TryGetProperty("200", out _));
    }

    [Fact]
    public async Task ShouldDescribeManualApplicationInventoryGivenOpenApi()
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
        var applications = paths.GetProperty("/api/v1/tenants/{tenant_id}/applications");
        var declare = applications.GetProperty("post");
        var declareSchema = declare.GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema");
        foreach (var name in new[] { "name", "purpose" })
        {
            Assert.True(declareSchema.GetProperty("properties").TryGetProperty(name, out _));
            Assert.Contains(declareSchema.GetProperty("required").EnumerateArray(),
                required => required.GetString() == name);
        }
        Assert.True(declare.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(applications.GetProperty("get").GetProperty("responses")
            .TryGetProperty("200", out _));
        var application = paths.GetProperty(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}");
        Assert.True(application.GetProperty("put").TryGetProperty("requestBody", out _));
        Assert.Contains(application.GetProperty("get").GetProperty("parameters").EnumerateArray(),
            parameter => parameter.GetProperty("name").GetString() == "minimum_revision" &&
                         parameter.GetProperty("in").GetString() == "query");
        var history = paths.GetProperty(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/revisions");
        var historyResponse = history.GetProperty("get").GetProperty("responses")
            .GetProperty("200").GetProperty("content").GetProperty("application/json")
            .GetProperty("schema");
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var historySchema = schemas.GetProperty(historyResponse.GetProperty("$ref")
            .GetString()!.Split('/')[^1]);
        Assert.True(historySchema.GetProperty("properties").TryGetProperty("next_cursor", out _));
        var historyItem = historySchema.GetProperty("properties").GetProperty("items")
            .GetProperty("items");
        Assert.True(historyItem.GetProperty("properties").TryGetProperty("change_kind", out _));
        Assert.Contains(history.GetProperty("get").GetProperty("parameters").EnumerateArray(),
            parameter => parameter.GetProperty("name").GetString() ==
                         "minimum_application_revision" &&
                         parameter.GetProperty("in").GetString() == "query");
        var exactResponse = paths.GetProperty(
                "/api/v1/tenants/{tenant_id}/applications/{application_id}/revisions/{revision}")
            .GetProperty("get").GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var exactSchema = schemas.GetProperty(exactResponse.GetProperty("$ref")
            .GetString()!.Split('/')[^1]);
        Assert.True(exactSchema.GetProperty("properties").TryGetProperty("system_instance_id", out _));
        Assert.True(exactSchema.GetProperty("properties").TryGetProperty("system_instance", out _));
        var instances = paths.GetProperty(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system_instances");
        var instanceSchema = instances.GetProperty("post").GetProperty("requestBody")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        foreach (var name in new[] { "expected_application_revision", "name", "kind" })
            Assert.Contains(instanceSchema.GetProperty("required").EnumerateArray(),
                required => required.GetString() == name);
        Assert.True(instanceSchema.GetProperty("properties")
            .TryGetProperty("source_identifier", out _));
        Assert.True(instances.GetProperty("get").GetProperty("responses")
            .TryGetProperty("200", out _));
        foreach (var operation in new[]
                 {
                     instances.GetProperty("get"),
                     paths.GetProperty(
                         "/api/v1/tenants/{tenant_id}/applications/{application_id}/system_instances/{system_instance_id}")
                         .GetProperty("get"),
                 })
        {
            Assert.True(operation.GetProperty("responses").TryGetProperty("409", out _));
            Assert.Contains(operation.GetProperty("parameters").EnumerateArray(),
                parameter => parameter.GetProperty("name").GetString() ==
                             "minimum_application_revision" &&
                             parameter.GetProperty("in").GetString() == "query");
        }
        Assert.True(paths.GetProperty(
                "/api/v1/tenants/{tenant_id}/applications/{application_id}/system_instances/{system_instance_id}")
            .GetProperty("get").GetProperty("responses").TryGetProperty("200", out _));
        foreach (var path in new[]
                 {
                     "/api/v1/tenants/{tenant_id}/applications/{application_id}/boundary_references",
                     "/api/v1/tenants/{tenant_id}/applications/{application_id}/system_instances/{system_instance_id}/boundary_references",
                 })
        {
            var operation = paths.GetProperty(path).GetProperty("get");
            Assert.True(operation.GetProperty("responses").TryGetProperty("200", out _));
            Assert.True(operation.GetProperty("responses").TryGetProperty("409", out _));
            Assert.Contains(operation.GetProperty("parameters").EnumerateArray(),
                parameter => parameter.GetProperty("name").GetString() == "limit" &&
                             parameter.GetProperty("in").GetString() == "query");
            Assert.Contains(operation.GetProperty("parameters").EnumerateArray(),
                parameter => parameter.GetProperty("name").GetString() == "cursor" &&
                             parameter.GetProperty("in").GetString() == "query");
        }
        var changePreview = paths.GetProperty(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/change_previews")
            .GetProperty("post");
        var previewSchema = changePreview.GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema");
        foreach (var name in new[] { "expected_application_revision", "change_kind" })
            Assert.True(previewSchema.GetProperty("properties").TryGetProperty(name, out _));
        Assert.True(changePreview.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(changePreview.GetProperty("responses").TryGetProperty("409", out _));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task ShouldUseSnakeCaseGivenApiRouteQueryAndJsonNames(string environment)
    {
        // Arrange
        await using var factory = CreateBrokerFreeFactory(environment);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/openapi/v1.json", CancellationToken.None);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(
            CancellationToken.None));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paths = document.RootElement.GetProperty("paths");
        if (environment == "Production")
            Assert.True(paths.TryGetProperty("/api/v1/my/oidc_identity_links", out _));
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

    [Fact]
    public async Task ShouldDescribeStageAndPreviewOnlyGivenApplicationImportOpenApi()
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
        var collection = paths.GetProperty("/api/v1/tenants/{tenant_id}/application_imports");
        var stage = collection.GetProperty("post");
        var body = stage.GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema");
        foreach (var name in new[] { "submission_id", "source_key", "source_namespace",
                     "coverage", "rows" })
            Assert.True(body.GetProperty("properties").TryGetProperty(name, out _));
        Assert.False(body.GetProperty("properties").TryGetProperty("tenant_id", out _));
        Assert.True(stage.GetProperty("responses").TryGetProperty("200", out _));
        var batch = paths.GetProperty("/api/v1/tenants/{tenant_id}/application_imports/{batch_id}");
        Assert.True(batch.TryGetProperty("get", out _));
        var cancellation = paths.GetProperty(
            "/api/v1/tenants/{tenant_id}/application_imports/{batch_id}/cancellations");
        Assert.True(cancellation.TryGetProperty("post", out var cancel));
        var cancelBody = cancel.GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema");
        Assert.True(cancelBody.GetProperty("properties")
            .TryGetProperty("expected_batch_revision", out _));
        Assert.True(cancelBody.GetProperty("properties").TryGetProperty("reason", out _));
        foreach (var suffix in new[] { "/rows", "/preview" })
        {
            var read = paths.GetProperty(
                $"/api/v1/tenants/{{tenant_id}}/application_imports/{{batch_id}}{suffix}")
                .GetProperty("get");
            Assert.Contains(read.GetProperty("parameters").EnumerateArray(), parameter =>
                parameter.GetProperty("name").GetString() == "minimum_revision");
        }
        Assert.DoesNotContain(paths.EnumerateObject(), path =>
            path.Name.Contains("acceptances", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldDescribeInvitationStatusGivenOpenApi()
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
        var operation = document.RootElement.GetProperty("paths")
            .GetProperty("/api/v1/tenants/{tenant_id}/member_invitations")
            .GetProperty("get");
        Assert.True(operation.GetProperty("responses").TryGetProperty("200", out _));
        var parameterNames = operation.GetProperty("parameters").EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString()).ToArray();
        Assert.Contains("email_address", parameterNames);
        Assert.DoesNotContain("expected_status", parameterNames);
    }

    [Fact]
    public async Task ShouldDescribeOperatorPortfolioGivenOpenApi()
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
        var operation = document.RootElement.GetProperty("paths")
            .GetProperty("/api/v1/platform/tenants").GetProperty("get");
        Assert.True(operation.GetProperty("responses").TryGetProperty("200", out _));
        var parameters = operation.GetProperty("parameters").EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString()).ToArray();
        Assert.Contains("limit", parameters);
        Assert.Contains("cursor", parameters);
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
