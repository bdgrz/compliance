using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ApplicationReplayE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldReplayApplicationAndSystemInstanceReadsAfterWorkerRestartGivenTwoTenants()
    {
        // Arrange: create separate application streams and let their initial projections catch up.
        var applicationName = $"application-replay-{Guid.NewGuid():N}";
        var worker = BuildWorker(applicationName);
        await worker.StartAsync();
        try
        {
            await using var factory = E2EAppFactory.Create(broker, applicationName);
            var previousMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
            HttpClient owner;
            try
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", "api");
                owner = factory.CreateClient();
            }
            finally
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", previousMode);
            }

            using (owner)
            {
                await TenantInvitationE2ETests.LoginAsync(owner,
                    $"application-replay-{Guid.NewGuid():N}@example.com");
                var first = await SeedAsync(owner, "Application Replay A");
                var second = await SeedAsync(owner, "Application Replay B");
                foreach (var scope in new[] { first, second })
                    _ = await WaitForHttpAsync(owner, scope.Path + "?minimum_revision=1",
                        document => document.GetProperty("revision").GetInt64() == 1);

                await worker.StopAsync();

                // Act: revise each application and register a system instance while its projector is stopped.
                foreach (var scope in new[] { first, second })
                {
                    using var revised = await owner.PutAsJsonAsync(scope.Path, new
                    {
                        expected_revision = 1,
                        name = scope.Label + " after worker restart",
                        purpose = "Updated while the Application projection worker is stopped",
                    });
                    Assert.Equal(HttpStatusCode.NoContent, revised.StatusCode);

                    using var stale = await owner.GetAsync(scope.Path + "?minimum_revision=2");
                    Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

                    using var registered = await owner.PostAsJsonAsync(
                        scope.Path + "/system-instances", new
                        {
                            expected_application_revision = 2,
                            name = scope.Label + " replay instance",
                            kind = "production",
                            source_identifier = scope.Label + "-replay-instance",
                        });
                    Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
                    scope.ReplayInstanceId = Guid.Parse((await ReadAsync(registered))
                        .GetProperty("system_instance_id").GetString()!);
                }

                // Act: a fresh worker with the same identity resumes both tenant checkpoints.
                var stoppedWorker = worker;
                worker = BuildWorker(applicationName);
                stoppedWorker.Dispose();
                await worker.StartAsync();

                // Assert: HTTP and MCP expose revision history, actor snapshots, and both instances.
                await using var mcp = await McpScenario.ConnectAsync(owner,
                    new Uri(owner.BaseAddress!, "/mcp"));
                foreach (var scope in new[] { first, second })
                {
                    var current = await WaitForHttpAsync(owner,
                        scope.Path + "?minimum_revision=2", document =>
                            document.GetProperty("revision").GetInt64() == 2);
                    Assert.Equal(scope.TenantId.ToString(),
                        current.GetProperty("tenant_id").GetString());
                    Assert.Equal(scope.ApplicationId.ToString(),
                        current.GetProperty("application_id").GetString());
                    Assert.Equal(scope.Label + " after worker restart",
                        current.GetProperty("name").GetString());
                    AssertActorSnapshot(current, "last_changed_by",
                        "last_changed_by_member_id", "last_changed_by_display");

                    var historyPath = scope.Path + "/revisions?minimum_application_revision=2";
                    var history = await WaitForHttpAsync(owner, historyPath, document =>
                        document.GetProperty("items").GetArrayLength() == 2);
                    AssertHistory(history.GetProperty("items"), scope);

                    var instanceListPath = scope.Path + "/system-instances?minimum_application_revision=2";
                    var instances = await WaitForHttpAsync(owner, instanceListPath, document =>
                        document.GetProperty("items").GetArrayLength() == 2);
                    AssertInstanceIds(instances.GetProperty("items"), scope);
                    AssertActorSnapshot(Assert.Single(instances.GetProperty("items")
                        .EnumerateArray(), item => item.GetProperty("system_instance_id")
                        .GetString() == scope.ReplayInstanceId.ToString()), "declared_by",
                        "declared_by_member_id", "declared_by_display");

                    var mcpCurrent = await ReadToolAsync(mcp, "bdgrz.application.get",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = scope.TenantId,
                            ["application_id"] = scope.ApplicationId,
                            ["minimum_revision"] = 2,
                        });
                    Assert.Equal(scope.TenantId.ToString(),
                        mcpCurrent.GetProperty("tenant_id").GetString());
                    Assert.Equal(scope.Label + " after worker restart",
                        mcpCurrent.GetProperty("name").GetString());
                    AssertActorSnapshot(mcpCurrent, "last_changed_by",
                        "last_changed_by_member_id", "last_changed_by_display");

                    var mcpHistory = await ReadToolAsync(mcp,
                        "bdgrz.application.revision.list", new Dictionary<string, object?>
                        {
                            ["tenant_id"] = scope.TenantId,
                            ["application_id"] = scope.ApplicationId,
                            ["minimum_application_revision"] = 2,
                        });
                    AssertHistory(mcpHistory.GetProperty("items"), scope);

                    var mcpInstances = await ReadToolAsync(mcp,
                        "bdgrz.system_instance.list", new Dictionary<string, object?>
                        {
                            ["tenant_id"] = scope.TenantId,
                            ["application_id"] = scope.ApplicationId,
                            ["minimum_application_revision"] = 2,
                        });
                    AssertInstanceIds(mcpInstances.GetProperty("items"), scope);
                }
            }
        }
        finally
        {
            await worker.StopAsync();
            worker.Dispose();
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

    static async Task<ReplayScope> SeedAsync(HttpClient owner, string label)
    {
        using var tenantResponse = await PostUntilAuthorizedAsync(owner, "/api/v1/tenants",
            new { name = label, slug = $"application-replay-{Guid.NewGuid():N}"[..24] });
        var tenantId = Guid.Parse((await ReadAsync(tenantResponse))
            .GetProperty("tenant_id").GetString()!);
        var applicationsPath = $"/api/v1/tenants/{tenantId}/applications";
        using var applicationResponse = await PostUntilAuthorizedAsync(owner, applicationsPath,
            new { name = label, purpose = "Prove replay after worker restart" });
        var applicationId = Guid.Parse((await ReadAsync(applicationResponse))
            .GetProperty("application_id").GetString()!);
        var applicationPath = applicationsPath + "/" + applicationId;
        _ = await WaitForHttpAsync(owner, applicationPath + "?minimum_revision=1",
            document => document.GetProperty("revision").GetInt64() == 1);
        using var instanceResponse = await PostUntilAuthorizedAsync(owner,
            applicationPath + "/system-instances", new
            {
                expected_application_revision = 1,
                name = label + " initial instance",
                kind = "production",
                source_identifier = label + "-initial-instance",
            });
        var instanceId = Guid.Parse((await ReadAsync(instanceResponse))
            .GetProperty("system_instance_id").GetString()!);
        return new ReplayScope(label, tenantId, applicationId, applicationPath, instanceId);
    }

    static async Task<HttpResponseMessage> PostUntilAuthorizedAsync(HttpClient client,
        string path, object body)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var response = await client.PostAsJsonAsync(path, body);
            if (response.StatusCode == HttpStatusCode.OK)
                return response;
            Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
                await response.Content.ReadAsStringAsync());
            response.Dispose();
            await Task.Delay(250);
        }
        throw new TimeoutException($"POST {path} remained unauthorized after tenant bootstrap.");
    }

    static async Task<JsonElement> WaitForHttpAsync(HttpClient owner, string path,
        Func<JsonElement, bool> predicate)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var document = await ReadAsync(response);
                if (predicate(document))
                    return document;
            }
            else
                Assert.True(response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound,
                    await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException($"HTTP read {path} did not reach the expected projection state.");
    }

    static async Task<JsonElement> ReadToolAsync(McpScenario mcp, string tool,
        Dictionary<string, object?> arguments)
    {
        var call = await mcp.When(tool, arguments).ExpectSuccess();
        return Assert.IsType<JsonElement>(call.StructuredJson).GetProperty("result");
    }

    static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        Assert.IsType<JsonElement>(await response.Content.ReadFromJsonAsync<JsonElement>());

    static void AssertHistory(JsonElement items, ReplayScope scope)
    {
        Assert.Equal([1L, 2L], items.EnumerateArray().Select(item =>
            item.GetProperty("revision").GetInt64()).Order());
        var second = Assert.Single(items.EnumerateArray(), item =>
            item.GetProperty("revision").GetInt64() == 2);
        Assert.Equal(scope.TenantId.ToString(), second.GetProperty("tenant_id").GetString());
        Assert.Equal(scope.ApplicationId.ToString(),
            second.GetProperty("application_id").GetString());
        Assert.Equal(scope.Label + " after worker restart", second.GetProperty("name").GetString());
        AssertActorSnapshot(second, "actor", "last_changed_by_member_id",
            "last_changed_by_display");
    }

    static void AssertInstanceIds(JsonElement items, ReplayScope scope)
    {
        Assert.Equal(new[] { scope.InitialInstanceId.ToString(), scope.ReplayInstanceId.ToString() }
                .Order(StringComparer.Ordinal),
            items.EnumerateArray().Select(item =>
                item.GetProperty("system_instance_id").GetString())
                .Order(StringComparer.Ordinal));
        Assert.All(items.EnumerateArray(), item =>
        {
            Assert.Equal(scope.TenantId.ToString(), item.GetProperty("tenant_id").GetString());
            Assert.Equal(scope.ApplicationId.ToString(),
                item.GetProperty("application_id").GetString());
        });
    }

    static void AssertActorSnapshot(JsonElement document, string actorKey,
        string memberIdKey, string displayKey)
    {
        var actor = document.GetProperty(actorKey);
        Assert.Equal("member", actor.GetProperty("kind").GetString());
        Assert.Equal(document.GetProperty(memberIdKey).GetString(), actor.GetProperty("id").GetString());
        Assert.Equal(document.GetProperty(displayKey).GetString(), actor.GetProperty("display").GetString());
    }

    sealed class ReplayScope(string label, Guid tenantId, Guid applicationId,
        string path, Guid initialInstanceId)
    {
        public string Label { get; } = label;
        public Guid TenantId { get; } = tenantId;
        public Guid ApplicationId { get; } = applicationId;
        public string Path { get; } = path;
        public Guid InitialInstanceId { get; } = initialInstanceId;
        public Guid ReplayInstanceId { get; set; }
    }
}
