using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>Proves old application-stream instance history against the new write path.</summary>
[Collection(ApplicationInventoryBrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class SystemInstanceCutoverE2ETests(BrokerStackFixture broker)
{
    static readonly DateTimeOffset LegacyDeclaredAt =
        new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldServeLegacyInstanceHistoryAndNewDeclarationsGivenCutoverHosts(
        bool splitHosts)
    {
        // Arrange
        var applicationName = $"compliance-instance-cutover-{Guid.NewGuid():N}";
        using var worker = splitHosts ? BuildWorker(applicationName) : null;
        if (worker is not null)
            await worker.StartAsync();
        try
        {
            await using var factory = E2EAppFactory.Create(broker, applicationName);
            using var owner = CreateClient(factory, splitHosts);
            await TenantInvitationE2ETests.LoginAsync(owner,
                $"cutover-owner-{Guid.NewGuid():N}@example.com");
            var tenantId = await CreateTenantAsync(owner, "Cutover tenant");
            var otherTenantId = await CreateTenantAsync(owner, "Other cutover tenant");
            var applicationsPath = $"/api/v1/tenants/{tenantId}/applications";
            var applicationId = await DeclareApplicationAsync(owner, applicationsPath);
            _ = await DeclareApplicationAsync(owner,
                $"/api/v1/tenants/{otherTenantId}/applications");
            var applicationPath = $"{applicationsPath}/{applicationId}";
            await WaitForOkAsync(owner, $"{applicationPath}?minimum_revision=1");
            var legacyId = Uuid.CreateVersion4();
            var legacyActor = Uuid.CreateVersion4();
            var store = factory.Services.GetRequiredService<IEventStore>();

            // Act
            // The pre-cutover binary appended instance declarations to the application stream.
            await store.AppendAsync(new EventStreamAddress(tenantId.ToString(), "applications",
                applicationId.ToString()), 1,
            [
                DomainEventSeed.Attach(new SystemInstanceDeclared(
                    Uuid.Parse(tenantId.ToString(), CultureInfo.InvariantCulture),
                    Uuid.Parse(applicationId.ToString(), CultureInfo.InvariantCulture),
                    legacyId, 2, "Legacy production", "production", null, "legacy-prod",
                    legacyActor, "Legacy writer", LegacyDeclaredAt),
                    Uuid.Parse(applicationId.ToString(), CultureInfo.InvariantCulture), 2),
            ]);
            var legacyPath = $"{applicationPath}/system-instances/{legacyId}";
            var legacy = await WaitForOkAsync(owner,
                $"{legacyPath}?minimum_application_revision=2&minimum_instance_revision=1");
            var revisionTwo = await WaitForOkAsync(owner, $"{applicationPath}/revisions/2");
            var revisionOne = await WaitForOkAsync(owner, $"{applicationPath}/revisions/1");
            var revise = owner.PutAsJsonAsync(applicationPath, new
            {
                expected_revision = 2,
                name = "Payroll",
                purpose = "Run monthly payroll",
            });
            var declare = owner.PostAsJsonAsync($"{applicationPath}/system-instances", new
            {
                expected_application_revision = 2,
                name = "Payroll staging",
                kind = "staging",
            });
            await Task.WhenAll(revise, declare);
            using var revised = await revise;
            using var declared = await declare;
            var newId = (await ReadAsync(declared)).GetProperty("system_instance_id")
                .GetString();
            await WaitForOkAsync(owner,
                $"{applicationPath}/system-instances/{newId}?minimum_instance_revision=1");
            var list = await WaitForOkAsync(owner,
                $"{applicationPath}/system-instances?minimum_application_revision=3");
            using var missingApplication = await owner.PostAsJsonAsync(
                $"{applicationsPath}/{Guid.NewGuid()}/system-instances", Declaration());
            using var otherTenantApplication = await owner.PostAsJsonAsync(
                $"/api/v1/tenants/{otherTenantId}/applications/{applicationId}/system-instances",
                Declaration());
            await using var mcp = await McpScenario.ConnectAsync(owner,
                new Uri(owner.BaseAddress!, "/mcp"));
            var mcpLegacy = await mcp.When("bdgrz.system_instance.get", InstanceArguments(
                tenantId, applicationId, legacyId, 1)).ExpectSuccess();
            var mcpFuture = await mcp.When("bdgrz.system_instance.get", InstanceArguments(
                tenantId, applicationId, legacyId, 2)).ExpectFailure("Conflict");
            _ = await mcp.When("bdgrz.system_instance.get", InstanceArguments(
                tenantId, applicationId, legacyId, 0)).ExpectFailure("Validation");
            var mcpHistory = await mcp.When("bdgrz.application.revision.get",
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantId,
                    ["application_id"] = applicationId,
                    ["revision"] = 2,
                }).ExpectSuccess();
            _ = await mcp.When("bdgrz.system_instance.declare", DeclarationArguments(
                tenantId, Guid.NewGuid())).ExpectFailure("NotFound");
            _ = await mcp.When("bdgrz.system_instance.declare", DeclarationArguments(
                otherTenantId, applicationId)).ExpectFailure("NotFound");

            // Assert
            AssertLegacy(legacy, legacyId, legacyActor);
            AssertLegacy(Assert.IsType<JsonElement>(mcpLegacy.StructuredJson)
                .GetProperty("result"), legacyId, legacyActor);
            Assert.True(Assert.IsType<JsonElement>(mcpFuture.Error)
                .GetProperty("isTransient").GetBoolean());
            foreach (var revision in new[]
                     {
                         revisionTwo,
                         Assert.IsType<JsonElement>(mcpHistory.StructuredJson)
                             .GetProperty("result"),
                     })
            {
                Assert.Equal("system_instance_declared",
                    revision.GetProperty("change_kind").GetString());
                Assert.Equal(legacyId.ToString(),
                    revision.GetProperty("system_instance_id").GetString());
                Assert.Equal(legacyActor.ToString(),
                    revision.GetProperty("last_changed_by_member_id").GetString());
                AssertLegacy(revision.GetProperty("system_instance"), legacyId, legacyActor);
            }
            Assert.Equal("declared", revisionOne.GetProperty("change_kind").GetString());
            Assert.Equal(HttpStatusCode.NoContent, revised.StatusCode);
            Assert.Equal(HttpStatusCode.OK, declared.StatusCode);
            var listed = list.GetProperty("items").EnumerateArray()
                .Select(static item => item.GetProperty("system_instance_id").GetString())
                .ToArray();
            Assert.Contains(legacyId.ToString(), listed);
            Assert.Contains(newId, listed);
            Assert.Equal(HttpStatusCode.NotFound, missingApplication.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, otherTenantApplication.StatusCode);
        }
        finally
        {
            if (worker is not null)
                await worker.StopAsync();
        }
    }

    static void AssertLegacy(JsonElement instance, Uuid legacyId, Uuid legacyActor)
    {
        Assert.Equal(legacyId.ToString(), instance.GetProperty("system_instance_id").GetString());
        Assert.Equal(legacyActor.ToString(),
            instance.GetProperty("declared_by_member_id").GetString());
        Assert.Equal("Legacy writer", instance.GetProperty("declared_by_display").GetString());
        Assert.Equal(LegacyDeclaredAt, instance.GetProperty("declared_at").GetDateTimeOffset());
        Assert.Equal("manual", instance.GetProperty("source_kind").GetString());
        Assert.Equal("legacy-prod", instance.GetProperty("source_identifier").GetString());
        Assert.Equal(2, instance.GetProperty("legacy_application_revision").GetInt64());
        Assert.Equal(1, instance.GetProperty("revision").GetInt64());
    }

    static object Declaration() => new
    {
        expected_application_revision = 1,
        name = "Hidden",
        kind = "production",
    };

    static Dictionary<string, object?> DeclarationArguments(Guid tenantId, Guid applicationId) =>
        new()
        {
            ["tenant_id"] = tenantId,
            ["application_id"] = applicationId,
            ["expected_application_revision"] = 1,
            ["name"] = "Hidden",
            ["kind"] = "production",
        };

    static Dictionary<string, object?> InstanceArguments(Guid tenantId, Guid applicationId,
        Uuid instanceId, long minimumInstanceRevision) => new()
        {
            ["tenant_id"] = tenantId,
            ["application_id"] = applicationId,
            ["system_instance_id"] = instanceId.ToString(),
            ["minimum_instance_revision"] = minimumInstanceRevision,
        };

    static HttpClient CreateClient(WebApplicationFactory<Program> factory, bool apiOnly)
    {
        if (!apiOnly)
            return factory.CreateClient();
        var previousMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
        try
        {
            Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", "api");
            return factory.CreateClient();
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", previousMode);
        }
    }

    IHost BuildWorker(string applicationName)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true)
            .AddWorkers();
        return builder.Build();
    }

    static async Task<Guid> CreateTenantAsync(HttpClient owner, string name)
    {
        using var response = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name,
            slug = $"cutover-{Guid.NewGuid():N}"[..24],
        });
        return Guid.Parse((await ReadAsync(response)).GetProperty("tenant_id").GetString()!,
            CultureInfo.InvariantCulture);
    }

    static async Task<Guid> DeclareApplicationAsync(HttpClient owner, string path)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        HttpStatusCode? lastStatus = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(path, new
            {
                name = "Payroll",
                purpose = "Run payroll",
            });
            if (response.StatusCode == HttpStatusCode.OK)
                return Guid.Parse((await ReadAsync(response)).GetProperty("application_id")
                    .GetString()!, CultureInfo.InvariantCulture);
            lastStatus = response.StatusCode;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException($"Application declaration stayed unauthorized: {lastStatus}.");
    }

    static async Task<JsonElement> WaitForOkAsync(HttpClient owner, string path)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        string? last = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
                return await ReadAsync(response);
            last = $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}";
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await Task.Delay(250);
        }
        throw new TimeoutException($"{path} did not become readable: {last}");
    }

    static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"{(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }
}
