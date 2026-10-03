using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.Features.Providers;

[Trait("Category", "WebIntegration")]
public sealed class ProviderHttpMcpTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRecordAndReviseAuthoredFactsGivenAuthorizedHttpOrMcpCaller(bool mcp)
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/developer-user-sessions", new Login("provider-author@example.com"), CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tenantId = Uuid.CreateVersion4();
        var content = new ProviderContent("Supplier", "Authored kind", "not_material", [], "No customer data or critical path");
        await using var scenario = mcp ? await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp")) : null;

        // Act
        ProviderRegistration registration;
        ProviderRegistration revised;
        if (scenario is not null)
        {
            var created = await scenario.When("bdgrz.provider.record", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId.ToString(),
                ["content"] = JsonSerializer.SerializeToElement(content, ComplianceCoreJsonContext.Default.ProviderContent),
            }).ExpectSuccess();
            registration = Result(created).Deserialize(ComplianceCoreJsonContext.Default.ProviderRegistration)!;
            var changed = await scenario.When("bdgrz.provider.revise", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId.ToString(),
                ["provider_id"] = registration.ProviderId.ToString(),
                ["expected_revision"] = 1,
                ["content"] = JsonSerializer.SerializeToElement(content with { Name = "Changed supplier" }, ComplianceCoreJsonContext.Default.ProviderContent),
            }).ExpectSuccess();
            revised = Result(changed).Deserialize(ComplianceCoreJsonContext.Default.ProviderRegistration)!;
        }
        else
        {
            using var body = Body(content);
            using var created = await client.PostAsync($"/api/v1/tenants/{tenantId}/providers", body);
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            registration = JsonSerializer.Deserialize(await created.Content.ReadAsStringAsync(), ComplianceCoreJsonContext.Default.ProviderRegistration)!;
            using var revisionBody = new StringContent("{\"expected_revision\":1,\"content\":" + JsonSerializer.Serialize(content with { Name = "Changed supplier" }, ComplianceCoreJsonContext.Default.ProviderContent) + "}", Encoding.UTF8, "application/json");
            using var changed = await client.PutAsync($"/api/v1/tenants/{tenantId}/providers/{registration.ProviderId}", revisionBody);
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            revised = JsonSerializer.Deserialize(await changed.Content.ReadAsStringAsync(), ComplianceCoreJsonContext.Default.ProviderRegistration)!;
        }
        var source = await ProgramManagementServices.HydrateAsync(factory.Services, new ProviderRegister(tenantId));
        var current = source.Get(registration.ProviderId)!;

        // Assert
        Assert.Equal(registration.ProviderId, revised.ProviderId);
        Assert.Equal(1, registration.Revision);
        Assert.Equal(2, revised.Revision);
        Assert.Equal("Changed supplier", current.Content.Name);
        Assert.Equal("Authored kind", current.Content.ProviderKind);
        Assert.Equal("member", current.RecordedBy.Kind);
        Assert.Equal("manual", current.SourceKind);
        Assert.Equal(["owner", "source_citation"], current.Unresolved);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReadEmptyTenantRegisterGivenAuthorizedHttpOrMcpCaller(bool mcp)
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/developer-user-sessions", new Login("provider-reader@example.com"), CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tenantId = Uuid.CreateVersion4();
        await using var scenario = mcp ? await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp")) : null;

        // Act
        JsonElement page;
        if (scenario is not null)
        {
            var listed = await scenario.When("bdgrz.providers.list", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId.ToString(),
                ["limit"] = 1,
            }).ExpectSuccess();
            page = Result(listed);
        }
        else
        {
            using var listed = await client.GetAsync($"/api/v1/tenants/{tenantId}/providers?limit=1");
            Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
            using var json = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
            page = json.RootElement.Clone();
        }

        // Assert
        Assert.Equal(0, page.GetProperty("items").GetArrayLength());
    }

    static JsonElement Result(McpCallSnapshot snapshot) => Assert.IsType<JsonElement>(snapshot.StructuredJson).GetProperty("result");

    static StringContent Body(ProviderContent content) => new("{\"content\":" + JsonSerializer.Serialize(content,
        ComplianceCoreJsonContext.Default.ProviderContent) + "}", Encoding.UTF8, "application/json");

    internal static WebApplicationFactory<Program> CreateFactory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
        builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
        builder.UseSetting("Fitz:ApplicationName", "provider-register-tests");
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                         descriptor.ImplementationType?.Name != "ComplianceReadinessLifecycle").ToArray())
                services.Remove(descriptor);
            var store = new InMemoryEventStore();
            services.RemoveAll<IEventStore>();
            services.RemoveAll<IDomainEventReader>();
            services.AddSingleton<IEventStore>(store);
            services.AddSingleton<IDomainEventReader>(store);
            services.RemoveAll<IKvClient>();
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.RemoveAll<ITenantMembershipDirectoryReader>();
            services.AddSingleton<ITenantMembershipDirectoryReader>(new FixedMembershipDirectory(true));
            services.RemoveAll<ITenantActivity>();
            services.AddSingleton<ITenantActivity, ActiveTenant>();
            services.RemoveAll<IAccessGrantPermissionAuthorizer>();
            services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(new RecordingPermissionAuthorizer(true)));
        });
    });

    sealed record Login([property: JsonPropertyName("email_address")] string EmailAddress);
}
