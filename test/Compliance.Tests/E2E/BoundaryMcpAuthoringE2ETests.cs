using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance.Features.Boundaries;
using Cntryl.Fitz;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class BoundaryMcpAuthoringE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldCreateReadAndReviseBoundaryThroughMcpGivenStandaloneOrSplitHost(
        bool splitHosts)
    {
        // Arrange
        var applicationName = $"compliance-boundary-mcp-{Guid.NewGuid():N}";
        var worker = splitHosts ? BuildWorker(applicationName) : null;
        try
        {
            if (worker is not null)
                await worker.StartAsync();
            await using var factory = E2EAppFactory.Create(broker, applicationName);
            var priorMode = TestHostMode.Current;
            HttpClient ownerClient;
            try
            {
                TestHostMode.Set(splitHosts ? "api" : "standalone");
                ownerClient = factory.CreateClient();
            }
            finally
            {
                TestHostMode.Set(priorMode);
            }

            using var owner = ownerClient;
            await TenantInvitationE2ETests.LoginAsync(owner,
                $"boundary-mcp-owner-{Guid.NewGuid():N}@example.com");
            using var tenantResponse = await owner.PostAsJsonAsync("/api/v1/tenants", new
            {
                name = "Boundary MCP authoring",
                slug = $"boundary-mcp-{Guid.NewGuid():N}"[..24],
            });
            Assert.Equal(HttpStatusCode.OK, tenantResponse.StatusCode);
            using var tenantDocument = JsonDocument.Parse(
                await tenantResponse.Content.ReadAsStringAsync());
            var tenantId = tenantDocument.RootElement.GetProperty("tenant_id").GetString()!;
            await AccessGrantE2ESupport.IssueFounderOrganizationGrantAsync(owner, tenantId);

            var programsPath = $"/api/v1/tenants/{tenantId}/programs";
            var plan = new
            {
                target_readiness_date = "2027-01-31",
                target_type_i_as_of_date = "2027-03-31",
                target_type_ii_start_date = "2027-04-01",
                target_type_ii_end_date = "2028-03-31",
                readiness_advisor = "Advisor",
                audit_firm = (string?)null,
            };
            var programId = await CreateProgramWhenReadyAsync(owner, programsPath, plan);
            await WaitForRevisionAsync(owner, $"{programsPath}/{programId}", 1);
            await using var mcp = await McpScenario.ConnectAsync(owner,
                new Uri(owner.BaseAddress!, "/mcp"));
            var entryId = Guid.NewGuid().ToString("D");
            var initialContent = Content("The customer service is in scope.", entryId);
            var revisedContent = Content("The customer service and provider are in scope.", entryId);

            // Act: author and read the exact draft through the machine-facing contract.
            var created = await mcp.When("bdgrz.boundary.create",
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantId,
                    ["program_id"] = programId,
                    ["content"] = initialContent,
                }).ExpectSuccess();
            var registration = Assert.IsType<JsonElement>(created.StructuredJson)
                .GetProperty("result");
            var boundaryId = registration.GetProperty("boundary_id").GetString()!;
            var draftVersionId = registration.GetProperty("draft_version_id").GetString()!;
            var boundaryPath = $"/api/v1/tenants/{tenantId}/boundaries/{boundaryId}";
            await WaitForRevisionAsync(owner, boundaryPath, 1);
            var getInput = new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["boundary_id"] = boundaryId,
                ["minimum_revision"] = 1,
            };
            var firstRead = await mcp.When("bdgrz.boundary.get", getInput).ExpectSuccess();
            var firstDraft = Assert.IsType<JsonElement>(firstRead.StructuredJson)
                .GetProperty("result").GetProperty("draft");
            Assert.Equal(draftVersionId, firstDraft.GetProperty("version_id").GetString());
            Assert.Equal(1, firstDraft.GetProperty("revision").GetInt64());
            Assert.Equal("The customer service is in scope.",
                firstDraft.GetProperty("content").GetProperty("statement").GetString());

            var reviseInput = new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["boundary_id"] = boundaryId,
                ["draft_version_id"] = draftVersionId,
                ["expected_revision"] = 1,
                ["content"] = revisedContent,
            };

            // Hold the exact boundary stream so this MCP edit loses a real broker
            // session race. Once released, the same edit must append exactly once.
            await using var blockingClient = await CreateFitzClientAsync(broker.WebSocketEndpoint);
            await using var blocker = await blockingClient.Stream.BeginAsync(
                new EventStreamAddress(tenantId, "boundaries", boundaryId).ToString());
            var contended = await mcp.When("bdgrz.boundary.draft.revise", reviseInput);
            var conflict = Assert.IsType<JsonElement>(contended.Error);
            Assert.True(contended.IsError);
            Assert.Equal("Conflict", conflict.GetProperty("kind").GetString());
            Assert.True(conflict.GetProperty("isTransient").GetBoolean());
            await blocker.RollbackAsync();

            _ = await mcp.When("bdgrz.boundary.draft.revise", reviseInput).ExpectSuccess();
            await WaitForRevisionAsync(owner, boundaryPath, 2);
            getInput["minimum_revision"] = 2;
            var revisedRead = await mcp.When("bdgrz.boundary.get", getInput).ExpectSuccess();
            var revisedDraft = Assert.IsType<JsonElement>(revisedRead.StructuredJson)
                .GetProperty("result").GetProperty("draft");

            // Assert: the revision and content changed once, and a stale replay is rejected.
            Assert.Equal(draftVersionId, revisedDraft.GetProperty("version_id").GetString());
            Assert.Equal(2, revisedDraft.GetProperty("revision").GetInt64());
            Assert.Equal("The customer service and provider are in scope.",
                revisedDraft.GetProperty("content").GetProperty("statement").GetString());
            _ = await mcp.When("bdgrz.boundary.draft.revise", reviseInput)
                .ExpectFailure("Conflict");
            var finalRead = await mcp.When("bdgrz.boundary.get", getInput).ExpectSuccess();
            Assert.Equal(2, Assert.IsType<JsonElement>(finalRead.StructuredJson)
                .GetProperty("result").GetProperty("draft").GetProperty("revision").GetInt64());
            await AssertExactBoundaryHistoryAsync(factory.Services.GetRequiredService<IEventStore>(),
                tenantId, boundaryId, revisedContentStatement:
                "The customer service and provider are in scope.");
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

    static async Task<string> CreateProgramWhenReadyAsync(HttpClient owner, string path, object plan)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        string? lastResponse = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(path,
                new { name = "MCP boundary program", plan });
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using var document = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync());
                return document.RootElement.GetProperty("program_id").GetString()!;
            }
            lastResponse = $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}";
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                lastResponse);
            await Task.Delay(250);
        }
        throw new TimeoutException($"Program creation did not become available: {lastResponse}");
    }

    static async Task WaitForRevisionAsync(HttpClient owner, string path, long revision)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        string? lastResponse = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync($"{path}?minimum_revision={revision}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using var document = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync());
                if (document.RootElement.GetProperty("revision").GetInt64() >= revision)
                    return;
            }
            lastResponse = $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}";
            Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict or
                HttpStatusCode.NotFound, lastResponse);
            await Task.Delay(250);
        }
        throw new TimeoutException($"{path} did not project revision {revision}: {lastResponse}");
    }

    static async Task<Client> CreateFitzClientAsync(string endpoint)
    {
        var client = new Client(new ClientConfig(new Uri(endpoint, UriKind.Absolute),
            Timeout: TimeSpan.FromSeconds(10)));
        try
        {
            await client.ConnectWhenReadyAsync(new ConnectWhenReadyOptions(TimeSpan.FromSeconds(15)));
            return client;
        }
        catch
        {
            await client.DisposeAsync();
            throw;
        }
    }

    static async Task AssertExactBoundaryHistoryAsync(IEventStore store, string tenantId,
        string boundaryId, string revisedContentStatement)
    {
        var events = new List<DomainEvent>();
        await foreach (var record in store.ReadAsync(
                           EventStreamPattern.ForPattern(tenantId, "boundaries", boundaryId),
                           EventCursor.Start, CancellationToken.None))
        {
            events.Add(record.Event);
        }
        Assert.Collection(events,
            created => Assert.IsType<BoundaryDraftCreated>(created),
            revision =>
            {
                var revised = Assert.IsType<BoundaryDraftRevised>(revision);
                Assert.Equal(2, revised.Revision);
                Assert.Equal(revisedContentStatement, revised.Content.Statement);
            });
    }

    static object Content(string statement, string entryId) => new
    {
        statement,
        engagement_stage = "readiness",
        trust_services_categories = new[] { "security" },
        entries = new[]
        {
            new
            {
                entry_id = entryId,
                kind = "question",
                subject_type = "service",
                subject = "Customer service",
                governed_record_id = (string?)null,
                owner_reference = "Compliance lead",
                rationale = "Confirm the provider before approval.",
                unresolved = true,
            },
        },
    };
}
