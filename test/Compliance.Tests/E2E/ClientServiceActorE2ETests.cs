using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ClientServiceActorE2ETests(BrokerStackFixture broker) : IClassFixture<BrokerStackFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldKeepActionActorGivenChangedDisplayInStandaloneAndSplitHosts(bool splitHosts)
    {
        // Arrange
        var applicationName = $"compliance-service-actor-{Guid.NewGuid():N}";
        IHost? worker = null;
        if (splitHosts)
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
            worker = builder.Build();
            await worker.StartAsync();
        }
        try
        {
            await using var factory = E2EAppFactory.Create(broker, applicationName);
            var previousMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
            HttpClient owner;
            try
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE",
                    splitHosts ? "api" : "standalone");
                owner = factory.CreateClient();
            }
            finally
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", previousMode);
            }
            using (owner)
            {
                var originalEmail = $"service-original-{Guid.NewGuid():N}@example.com";
                var revisedEmail = $"service-revised-{Guid.NewGuid():N}@example.com";
                var retiredEmail = $"service-retired-{Guid.NewGuid():N}@example.com";
                var userId = await TenantInvitationE2ETests.LoginAsync(owner, originalEmail);
                using var tenantResponse = await owner.PostAsJsonAsync("/api/v1/tenants", new
                {
                    name = "Service actor tenant",
                    slug = $"service-actor-{Guid.NewGuid():N}"[..24],
                });
                Assert.Equal(HttpStatusCode.OK, tenantResponse.StatusCode);
                var tenantId = (await ReadJsonAsync(tenantResponse)).GetProperty("tenant_id")
                    .GetString()!;
                var memberId = RbacIds.Member(
                    Uuid.Parse(tenantId, CultureInfo.InvariantCulture),
                    Uuid.Parse(userId, CultureInfo.InvariantCulture)).ToString();
                var programId = await CreateProgramAsync(owner, tenantId);
                var serviceId = await CreateServiceAsync(owner, tenantId, programId);
                var servicePath = $"/api/v1/tenants/{tenantId}/client-services/{serviceId}";
                var createdView = await WaitForCurrentAsync(owner, servicePath, 1);
                AssertActor(createdView.GetProperty("last_changed_by"), memberId, originalEmail);

                // Act: a second developer identity changes the cookie display, while the
                // platform user and tenant member remain the same.
                Assert.Equal(userId, await TenantInvitationE2ETests.LoginAsync(owner, revisedEmail));
                using var revised = await owner.PutAsJsonAsync(servicePath, new
                {
                    expected_revision = 1,
                    name = "Payroll",
                    purpose = "Monthly payroll",
                    owner_reference = "Finance",
                });
                Assert.Equal(HttpStatusCode.NoContent, revised.StatusCode);
                var revisedView = await WaitForCurrentAsync(owner, servicePath, 2);
                AssertActor(revisedView.GetProperty("last_changed_by"), memberId, revisedEmail);
                Assert.Equal(userId, await TenantInvitationE2ETests.LoginAsync(owner, retiredEmail));
                using var retired = await owner.PostAsJsonAsync($"{servicePath}/retirements",
                    new { expected_revision = 2, rationale = "Service ended" });
                Assert.Equal(HttpStatusCode.NoContent, retired.StatusCode);

                // Assert: the latest actor changes, but each immutable revision keeps the
                // display captured when its event was written.
                var current = await WaitForCurrentAsync(owner, servicePath, 3);
                var revisions = await WaitForHistoryAsync(owner, servicePath, 3);
                Assert.Equal("retired", current.GetProperty("status").GetString());
                AssertActor(current.GetProperty("last_changed_by"), memberId, retiredEmail);
                using var currentListResponse = await owner.GetAsync(
                    $"/api/v1/tenants/{tenantId}/client-services");
                Assert.Equal(HttpStatusCode.OK, currentListResponse.StatusCode);
                var currentList = (await ReadJsonAsync(currentListResponse)).GetProperty("items");
                AssertActor(Assert.Single(currentList.EnumerateArray(),
                        item => item.GetProperty("service_id").GetString() == serviceId)
                    .GetProperty("last_changed_by"), memberId, retiredEmail);
                Assert.Equal([1L, 2L, 3L], revisions.Select(item => item.GetProperty("revision")
                    .GetInt64()));
                AssertActor(revisions[0].GetProperty("actor"), memberId, originalEmail);
                AssertActor(revisions[1].GetProperty("actor"), memberId, revisedEmail);
                AssertActor(revisions[2].GetProperty("actor"), memberId, retiredEmail);
                for (var revision = 1; revision <= 3; revision++)
                {
                    using var exactResponse = await owner.GetAsync($"{servicePath}/revisions/{revision}");
                    Assert.Equal(HttpStatusCode.OK, exactResponse.StatusCode);
                    var exact = await ReadJsonAsync(exactResponse);
                    AssertActor(exact.GetProperty("actor"), memberId,
                        new[] { originalEmail, revisedEmail, retiredEmail }[revision - 1]);
                }

                var stored = new List<DomainEvent>();
                await foreach (var record in factory.Services.GetRequiredService<IEventStore>()
                                   .ReadAsync(new EventStreamAddress(tenantId, "client-services",
                                           serviceId), 0, CancellationToken.None))
                    stored.Add(record.Event);
                Assert.Collection(stored,
                    ev => Assert.Equal(ActorReference.ForMember(
                        Uuid.Parse(memberId, CultureInfo.InvariantCulture), originalEmail),
                        Assert.IsType<ClientServiceCreated>(ev).StoredActor),
                    ev => Assert.Equal(ActorReference.ForMember(
                        Uuid.Parse(memberId, CultureInfo.InvariantCulture), revisedEmail),
                        Assert.IsType<ClientServiceRevised>(ev).StoredActor),
                    ev => Assert.Equal(ActorReference.ForMember(
                        Uuid.Parse(memberId, CultureInfo.InvariantCulture), retiredEmail),
                        Assert.IsType<ClientServiceRetired>(ev).StoredActor));

                await using var mcp = await McpScenario.ConnectAsync(owner,
                    new Uri(owner.BaseAddress!, "/mcp"));
                var input = new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantId,
                    ["service_id"] = serviceId,
                };
                var mcpCurrent = Assert.IsType<JsonElement>((await mcp.When(
                    "bdgrz.client-service.get", input).ExpectSuccess()).StructuredJson)
                    .GetProperty("result");
                AssertActor(mcpCurrent.GetProperty("last_changed_by"), memberId, retiredEmail);
                var mcpCurrentList = Assert.IsType<JsonElement>((await mcp.When(
                    "bdgrz.client-service.list", new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenantId,
                    }).ExpectSuccess()).StructuredJson).GetProperty("result").GetProperty("items");
                AssertActor(Assert.Single(mcpCurrentList.EnumerateArray(),
                        item => item.GetProperty("service_id").GetString() == serviceId)
                    .GetProperty("last_changed_by"), memberId, retiredEmail);
                var mcpHistory = Assert.IsType<JsonElement>((await mcp.When(
                    "bdgrz.client-service.revisions.list", input).ExpectSuccess()).StructuredJson)
                    .GetProperty("result").GetProperty("items");
                Assert.Equal(3, mcpHistory.GetArrayLength());
                AssertActor(mcpHistory[0].GetProperty("actor"), memberId, originalEmail);
                AssertActor(mcpHistory[1].GetProperty("actor"), memberId, revisedEmail);
                AssertActor(mcpHistory[2].GetProperty("actor"), memberId, retiredEmail);
                var mcpExactInput = new Dictionary<string, object?>(input) { ["revision"] = 1 };
                var mcpExact = Assert.IsType<JsonElement>((await mcp.When(
                    "bdgrz.client-service.revision.get", mcpExactInput).ExpectSuccess())
                    .StructuredJson).GetProperty("result");
                AssertActor(mcpExact.GetProperty("actor"), memberId, originalEmail);

                // Rehydrate from persisted events after all three actions. No member
                // directory lookup participates in replay or the history projection.
                await using var scope = factory.Services.CreateAsyncScope();
                var aggregate = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
                    .HydrateAsync(new ClientService(
                        Uuid.Parse(tenantId, CultureInfo.InvariantCulture),
                        Uuid.Parse(serviceId, CultureInfo.InvariantCulture)));
                Assert.Equal(3, aggregate.Revision);
                Assert.False(aggregate.IsActive);
            }
        }
        finally
        {
            if (worker is not null)
            {
                await worker.StopAsync();
                worker.Dispose();
            }
        }
    }

    static async Task<string> CreateProgramAsync(HttpClient owner, string tenantId)
    {
        var path = $"/api/v1/tenants/{tenantId}/programs";
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(path, new
            {
                name = "Service actor program",
                plan = new
                {
                    target_readiness_date = "2027-01-31",
                    target_type_i_as_of_date = "2027-03-31",
                    target_type_ii_start_date = "2027-04-01",
                    target_type_ii_end_date = "2028-03-31",
                    readiness_advisor = "Advisor",
                    audit_firm = (string?)null,
                },
            });
            if (response.StatusCode == HttpStatusCode.OK)
                return (await ReadJsonAsync(response)).GetProperty("program_id").GetString()!;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("Program management did not become available.");
    }

    static async Task<string> CreateServiceAsync(HttpClient owner, string tenantId,
        string programId)
    {
        var path = $"/api/v1/tenants/{tenantId}/programs/{programId}/client-services";
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(path, new
            {
                name = "Payroll",
                purpose = "Process payroll",
                owner_reference = "Operations",
            });
            if (response.StatusCode == HttpStatusCode.OK)
                return (await ReadJsonAsync(response)).GetProperty("service_id").GetString()!;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("Service management did not become available.");
    }

    static async Task<JsonElement> WaitForCurrentAsync(HttpClient owner, string path,
        long revision)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var current = await ReadJsonAsync(response);
                if (current.GetProperty("revision").GetInt64() == revision)
                    return current;
            }
            await Task.Delay(250);
        }
        throw new TimeoutException($"Service revision {revision} did not project.");
    }

    static async Task<JsonElement[]> WaitForHistoryAsync(HttpClient owner, string path,
        int count)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync($"{path}/revisions");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var items = (await ReadJsonAsync(response)).GetProperty("items")
                    .EnumerateArray().ToArray();
                if (items.Length == count)
                    return items;
            }
            await Task.Delay(250);
        }
        throw new TimeoutException($"Service history did not reach {count} revisions.");
    }

    static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    static void AssertActor(JsonElement actor, string memberId, string display)
    {
        Assert.Equal("member", actor.GetProperty("kind").GetString());
        Assert.Equal(memberId, actor.GetProperty("id").GetString());
        Assert.Equal(display, actor.GetProperty("display").GetString());
    }
}
