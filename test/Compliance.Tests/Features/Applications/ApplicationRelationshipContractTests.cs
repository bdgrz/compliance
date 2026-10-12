using System.Text.Json;
using Bdgrz.Compliance;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.Features.Applications;

[Trait("Category", "WebIntegration")]
public sealed class ApplicationRelationshipContractTests
{
    [Fact]
    public async Task ShouldExposeDirectedRelationshipRoutesGivenOpenApi()
    {
        // Arrange
        await using var factory = CreateBrokerFreeFactory();
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var paths = document.RootElement.GetProperty("paths");
        var collection = paths.GetProperty(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/relationships");
        var resource = paths.GetProperty(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/relationships/{relationship_id}");
        Assert.True(collection.TryGetProperty("get", out var list));
        Assert.True(resource.TryGetProperty("post", out var add));
        Assert.True(resource.TryGetProperty("delete", out var remove));

        Assert.Equal("listApplicationRelationships", list.GetProperty("operationId").GetString());
        Assert.Equal("addApplicationRelationship", add.GetProperty("operationId").GetString());
        Assert.Equal("removeApplicationRelationship", remove.GetProperty("operationId").GetString());
        var description = list.GetProperty("description").GetString();
        Assert.Contains("minimum_revision is a read-your-own-writes lower bound", description,
            StringComparison.Ordinal);
        Assert.Contains("current canonical relationship snapshot", description,
            StringComparison.Ordinal);
        Assert.Contains("retryable 409", description, StringComparison.Ordinal);
        Assert.Contains("requested and current relationship revisions", description,
            StringComparison.Ordinal);
        Assert.Contains("never returns a lower snapshot", description, StringComparison.Ordinal);
        Assert.Contains("reconstructs an exact historical revision", description,
            StringComparison.Ordinal);

        var addSchema = ResolveSchema(document.RootElement, add.GetProperty("requestBody")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema"));
        AssertRequiredProperties(addSchema, "expected_relationship_revision",
            "target_application_id", "relationship_type");
        Assert.Contains(remove.GetProperty("parameters").EnumerateArray(), parameter =>
            parameter.GetProperty("name").GetString() == "expected_relationship_revision" &&
            parameter.GetProperty("in").GetString() == "query" &&
            parameter.GetProperty("required").GetBoolean());
        var listParameters = list.GetProperty("parameters").EnumerateArray().ToArray();
        foreach (var name in new[] { "limit", "cursor", "minimum_revision" })
            Assert.Contains(listParameters, parameter =>
                parameter.GetProperty("name").GetString() == name &&
                parameter.GetProperty("in").GetString() == "query");

        var changeSchema = ResolveResponseSchema(document.RootElement, add);
        AssertProperties(changeSchema, "relationship_id", "relationship_revision", "state");
        var pageSchema = ResolveResponseSchema(document.RootElement, list);
        AssertProperties(pageSchema, "relationship_revision", "items", "next_cursor");
        var itemSchema = ResolveSchema(document.RootElement, pageSchema.GetProperty("properties")
            .GetProperty("items").GetProperty("items"));
        AssertProperties(itemSchema, "relationship_id", "target_application_id",
            "relationship_type", "actor_member_id", "actor_display", "changed_at");
    }

    static void AssertRequiredProperties(JsonElement schema, params string[] properties)
    {
        AssertProperties(schema, properties);
        var required = schema.GetProperty("required").EnumerateArray()
            .Select(item => item.GetString()).ToArray();
        foreach (var property in properties)
            Assert.Contains(property, required);
    }

    static void AssertProperties(JsonElement schema, params string[] properties)
    {
        var actual = schema.GetProperty("properties");
        foreach (var property in properties)
            Assert.True(actual.TryGetProperty(property, out _),
                $"OpenAPI schema is missing snake_case property '{property}'.");
    }

    static JsonElement ResolveResponseSchema(JsonElement root, JsonElement operation) =>
        ResolveSchema(root, operation.GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema"));

    static JsonElement ResolveSchema(JsonElement root, JsonElement schema)
    {
        while (schema.TryGetProperty("$ref", out var reference))
        {
            var name = reference.GetString()!.Split('/').Last();
            schema = root.GetProperty("components").GetProperty("schemas").GetProperty(name);
        }
        return schema;
    }

    static WebApplicationFactory<Program> CreateBrokerFreeFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Compliance:Authentication:Mode", "Development");
            builder.UseSetting("Compliance:Authentication:Authority", "https://issuer.example/");
            builder.UseSetting("Compliance:Authentication:Audience", "compliance-api");
            builder.UseSetting("Compliance:Authentication:ClientId", "compliance-spa");
            builder.UseSetting("Compliance:Authentication:Scopes", "openid profile");
            builder.UseSetting("BDGRZ_SESSION_SIGNING_KEY", "bdgrz-test-session-signing-key-000001");
            builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
            builder.UseSetting("Fitz:ApplicationName", "compliance-tests");
            builder.ConfigureServices(services =>
            {
                var brokerServices = services
                    .Where(descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                        descriptor.ImplementationType?.Name != "ComplianceReadinessLifecycle")
                    .ToArray();
                foreach (var descriptor in brokerServices)
                    services.Remove(descriptor);
            });
        });
}
