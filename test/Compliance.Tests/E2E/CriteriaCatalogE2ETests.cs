using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class CriteriaCatalogE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReadCatalogAndSelectExactEditionGivenHostMode(bool splitHosts)
    {
        // Arrange
        var applicationName = $"compliance-criteria-{Guid.NewGuid():N}";
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
            var priorMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
            HttpClient owner;
            try
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE",
                    splitHosts ? "api" : "standalone");
                owner = factory.CreateClient();
            }
            finally
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", priorMode);
            }
            using (owner)
            {
                await TenantInvitationE2ETests.LoginAsync(owner,
                    $"criteria-owner-{Guid.NewGuid():N}@example.com");
                using var tenantResponse = await owner.PostAsJsonAsync("/api/v1/tenants", new
                {
                    name = "Criteria catalog",
                    slug = $"criteria-{Guid.NewGuid():N}"[..24],
                });
                Assert.Equal(HttpStatusCode.OK, tenantResponse.StatusCode);
                var tenantId = (await ReadJsonAsync(tenantResponse)).GetProperty("tenant_id")
                    .GetString()!;
                var tenantPath = $"/api/v1/tenants/{tenantId}";
                var programId = await CreateProgramAsync(owner, tenantPath + "/programs");
                var programPath = $"{tenantPath}/programs/{programId}";
                await WaitForProgramAsync(owner, programPath, 1);
                await using var mcp = await McpScenario.ConnectAsync(owner,
                    new Uri(owner.BaseAddress!, "/mcp"));

                // Act: read the platform catalog through HTTP and MCP.
                var editions = await ReadJsonAsync(await owner.GetAsync(
                    tenantPath + "/criteria-editions"));
                var edition = Assert.Single(editions.EnumerateArray());
                var editionId = edition.GetProperty("edition_id").GetString()!;
                var editionPath = $"{tenantPath}/criteria-editions/{editionId}";
                var exactEdition = await ReadJsonAsync(await owner.GetAsync(editionPath));
                var entries = new List<JsonElement>();
                string? cursor = null;
                do
                {
                    var page = await ReadJsonAsync(await owner.GetAsync(editionPath +
                        "/entries?limit=25" + (cursor is null ? "" : "&cursor=" + cursor)));
                    entries.AddRange(page.GetProperty("items").EnumerateArray());
                    cursor = page.TryGetProperty("next_cursor", out var next) &&
                             next.ValueKind == JsonValueKind.String ? next.GetString() : null;
                } while (cursor is not null);
                var availability = await ReadJsonAsync(await owner.GetAsync(editionPath +
                    "/entries?category=availability&kind=criterion"));
                var focus = await ReadJsonAsync(await owner.GetAsync(editionPath +
                    "/entries?parent_identifier=CC6.1&kind=point_of_focus"));
                var criterion = await ReadJsonAsync(await owner.GetAsync(editionPath +
                    "/entries/CC6.1"));
                using var missingEntry = await owner.GetAsync(editionPath + "/entries/CC6.9");
                using var invalidFilter = await owner.GetAsync(editionPath +
                    "/entries?category=governance");
                using var missingEdition = await owner.GetAsync(
                    $"{tenantPath}/criteria-editions/{Uuid.CreateVersion4()}");
                var mcpEditions = await mcp.When("bdgrz.criteria.editions.list",
                    new Dictionary<string, object?> { ["tenant_id"] = tenantId }).ExpectSuccess();
                var mcpEntries = await mcp.When("bdgrz.criteria.entries.list",
                    new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenantId,
                        ["edition_id"] = editionId,
                        ["category"] = "privacy",
                        ["kind"] = "criterion",
                        ["limit"] = 200,
                    }).ExpectSuccess();
                var mcpEntry = await mcp.When("bdgrz.criteria.entry.get",
                    new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenantId,
                        ["edition_id"] = editionId,
                        ["identifier"] = "PI1.3",
                    }).ExpectSuccess();

                // Assert: the catalog is the exact seeded edition with original summaries only.
                Assert.Equal("2017_tsc_2022_pof", edition.GetProperty("edition_label").GetString());
                Assert.True(edition.GetProperty("is_complete").GetBoolean());
                Assert.Equal("identifiers_and_original_summaries",
                    edition.GetProperty("content_rights").GetString());
                Assert.Contains(edition.GetProperty("support_gaps").EnumerateArray(), gap =>
                    gap.GetProperty("category").GetString() == "privacy" &&
                    gap.GetProperty("code").GetString() == "privacy_lifecycle_unsupported");
                Assert.Equal(editionId, exactEdition.GetProperty("edition_id").GetString());
                Assert.Equal(61, entries.Count(static entry =>
                    entry.GetProperty("kind").GetString() == "criterion"));
                Assert.All(entries, entry => Assert.False(entry.TryGetProperty("text", out _)));
                Assert.Equal(3, availability.GetProperty("items").GetArrayLength());
                Assert.Equal("bdgrz:focus:cc6-1:asset-inventory",
                    Assert.Single(focus.GetProperty("items").EnumerateArray())
                        .GetProperty("identifier").GetString());
                Assert.Equal("CC6.1", criterion.GetProperty("source_identifier").GetString());
                Assert.Equal(HttpStatusCode.NotFound, missingEntry.StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, invalidFilter.StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, missingEdition.StatusCode);
                Assert.Equal(editionId, Assert.Single(Result(mcpEditions).EnumerateArray())
                    .GetProperty("edition_id").GetString());
                Assert.Equal(18, Result(mcpEntries).GetProperty("items").GetArrayLength());
                Assert.Equal("processing_integrity",
                    Result(mcpEntry).GetProperty("category").GetString());

                // Act: select concurrently with a plan revision at the same expected revision.
                var plan = Plan();
                var selectTask = owner.PutAsJsonAsync(programPath + "/criteria-edition",
                    new { expected_revision = 1, edition_id = editionId });
                var reviseTask = owner.PutAsJsonAsync(programPath,
                    new { expected_revision = 1, name = "Criteria program revised", plan });
                using var raceSelect = await selectTask;
                using var raceRevise = await reviseTask;

                // Assert: exactly one writer wins; the loser gets a version conflict.
                Assert.Equal(
                    new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict },
                    new[] { raceSelect.StatusCode, raceRevise.StatusCode }.Order());
                if (raceSelect.StatusCode == HttpStatusCode.Conflict)
                {
                    using var afterRevise = await owner.PutAsJsonAsync(
                        programPath + "/criteria-edition",
                        new { expected_revision = 2, edition_id = editionId });
                    Assert.Equal(HttpStatusCode.NoContent, afterRevise.StatusCode);
                }
                else
                {
                    using var afterSelect = await owner.PutAsJsonAsync(programPath,
                        new { expected_revision = 2, name = "Criteria program revised", plan });
                    Assert.Equal(HttpStatusCode.NoContent, afterSelect.StatusCode);
                }

                // Act: retry the selection (HTTP and MCP) and probe invalid selections.
                using var httpRetry = await owner.PutAsJsonAsync(programPath + "/criteria-edition",
                    new { expected_revision = 1, edition_id = editionId });
                _ = await mcp.When("bdgrz.program.criteria.select",
                    new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenantId,
                        ["program_id"] = programId,
                        ["expected_revision"] = 1,
                        ["edition_id"] = editionId,
                    }).ExpectSuccess();
                using var unknownEdition = await owner.PutAsJsonAsync(
                    programPath + "/criteria-edition",
                    new { expected_revision = 3, edition_id = Uuid.CreateVersion4() });
                using var missingProgram = await owner.PutAsJsonAsync(
                    $"{tenantPath}/programs/{Uuid.CreateVersion4()}/criteria-edition",
                    new { expected_revision = 1, edition_id = editionId });
                await WaitForProgramAsync(owner, programPath, 3);
                var projected = await ReadJsonAsync(await owner.GetAsync(
                    programPath + "?minimum_revision=3"));
                var history = await ReadJsonAsync(await owner.GetAsync(
                    programPath + "/revisions?minimum_program_revision=3&limit=10"));
                var mcpProgram = await mcp.When("bdgrz.program.get",
                    new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenantId,
                        ["program_id"] = programId,
                        ["minimum_revision"] = 3,
                    }).ExpectSuccess();
                var selections = new List<ProgramCriteriaEditionSelected>();
                var store = (worker?.Services ?? factory.Services)
                    .GetRequiredService<IEventStore>();
                await foreach (var record in store.ReadAsync(new ComplianceProgram(
                                       Uuid.Parse(tenantId, CultureInfo.InvariantCulture),
                                       Uuid.Parse(programId, CultureInfo.InvariantCulture)).Stream,
                                   0, CancellationToken.None))
                    if (record.Event is ProgramCriteriaEditionSelected selection)
                        selections.Add(selection);

                // Assert: retries are idempotent and the projection preserves the exact edition.
                Assert.Equal(HttpStatusCode.NoContent, httpRetry.StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, unknownEdition.StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, missingProgram.StatusCode);
                var stored = Assert.Single(selections);
                Assert.Equal(editionId, stored.EditionId.ToString());
                Assert.Equal("member", stored.StoredActor?.Kind);
                Assert.Equal(3, projected.GetProperty("revision").GetInt64());
                Assert.Equal(editionId, projected.GetProperty("criteria_edition_id").GetString());
                Assert.Equal(editionId, Result(mcpProgram).GetProperty("criteria_edition_id")
                    .GetString());
                var revisions = history.GetProperty("items").EnumerateArray()
                    .ToDictionary(static item => item.GetProperty("revision").GetInt64());
                Assert.Equal(JsonValueKind.Null,
                    revisions[1].GetProperty("criteria_edition_id").ValueKind);
                Assert.Equal(editionId, revisions[3].GetProperty("criteria_edition_id").GetString());
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

    static JsonElement Result(McpCallSnapshot result) =>
        Assert.IsType<JsonElement>(result.StructuredJson).GetProperty("result");

    static async Task<string> CreateProgramAsync(HttpClient owner, string path)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(path,
                new { name = "Criteria program", plan = Plan() });
            if (response.StatusCode == HttpStatusCode.OK)
                return (await ReadJsonAsync(response)).GetProperty("program_id").GetString()!;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("Program creation remained unauthorized after tenant bootstrap.");
    }

    static async Task WaitForProgramAsync(HttpClient client, string path, long revision)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync($"{path}?minimum_revision={revision}");
            if (response.StatusCode == HttpStatusCode.OK)
                return;
            await Task.Delay(250);
        }
        throw new TimeoutException($"The program projection did not reach revision {revision}.");
    }

    static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return document.RootElement.Clone();
        }
    }

    static object Plan() => new
    {
        target_readiness_date = "2027-01-31",
        target_type_i_as_of_date = "2027-03-31",
        target_type_ii_start_date = "2027-04-01",
        target_type_ii_end_date = "2028-03-31",
        readiness_advisor = "Criteria advisor",
        audit_firm = (string?)null,
    };
}
