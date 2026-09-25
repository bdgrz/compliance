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
public sealed class CriteriaReadLeakMatrixE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectTenantAndProgramSwapsGivenCatalogAndSelectionProbes(
        bool splitHosts)
    {
        // Arrange
        var applicationName = $"compliance-criteria-matrix-{Guid.NewGuid():N}";
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
            HttpClient outsider;
            try
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE",
                    splitHosts ? "api" : "standalone");
                owner = factory.CreateClient();
                outsider = factory.CreateClient();
            }
            finally
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", priorMode);
            }
            using (owner)
            using (outsider)
            {
                await TenantInvitationE2ETests.LoginAsync(owner,
                    $"criteria-matrix-owner-{Guid.NewGuid():N}@example.com");
                await TenantInvitationE2ETests.LoginAsync(outsider,
                    $"criteria-matrix-outsider-{Guid.NewGuid():N}@example.com");
                var tenantA = await CreateTenantAsync(owner, "Criteria matrix A");
                var tenantB = await CreateTenantAsync(owner, "Criteria matrix B");
                var programA = await CreateProgramAsync(owner, tenantA);
                var programB = await CreateProgramAsync(owner, tenantB);
                var editions = await ReadJsonAsync(await owner.GetAsync(
                    TenantPath(tenantA) + "/criteria-editions"));
                var editionId = Assert.Single(editions.EnumerateArray())
                    .GetProperty("edition_id").GetString()!;
                using (var selected = await owner.PutAsJsonAsync(
                           $"{TenantPath(tenantA)}/programs/{programA}/criteria-edition",
                           new { expected_revision = 1, edition_id = editionId }))
                    Assert.Equal(HttpStatusCode.NoContent, selected.StatusCode);
                var firstPage = await ReadJsonAsync(await owner.GetAsync(
                    $"{TenantPath(tenantA)}/criteria-editions/{editionId}/entries?limit=5"));
                var tenantACursor = firstPage.GetProperty("next_cursor").GetString()!;
                await using var ownerMcp = await McpScenario.ConnectAsync(owner,
                    new Uri(owner.BaseAddress!, "/mcp"));
                await using var outsiderMcp = await McpScenario.ConnectAsync(outsider,
                    new Uri(outsider.BaseAddress!, "/mcp"));

                // Act: an unrelated user cannot discover tenant A's catalog reads,
                // its program, or select an edition for it.
                var tenantAPath = TenantPath(tenantA);
                foreach (var path in new[]
                         {
                             tenantAPath + "/criteria-editions",
                             $"{tenantAPath}/criteria-editions/{editionId}",
                             $"{tenantAPath}/criteria-editions/{editionId}/entries",
                             $"{tenantAPath}/criteria-editions/{editionId}/entries/CC6.1",
                             $"{tenantAPath}/programs/{programA}",
                         })
                {
                    using var denied = await outsider.GetAsync(path);
                    Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
                }
                foreach (var (tool, input) in CatalogProbes(tenantA, editionId))
                    _ = await outsiderMcp.When(tool, input).ExpectFailure("NotFound");
                using (var outsiderSelect = await outsider.PutAsJsonAsync(
                           $"{tenantAPath}/programs/{programA}/criteria-edition",
                           new { expected_revision = 2, edition_id = editionId }))
                    Assert.Equal(HttpStatusCode.NotFound, outsiderSelect.StatusCode);
                _ = await outsiderMcp.When("bdgrz.program.criteria.select",
                    SelectInput(tenantA, programA, editionId)).ExpectFailure("NotFound");

                // A member of both tenants cannot select through a swapped tenant or program.
                var tenantBPath = TenantPath(tenantB);
                using (var swapped = await owner.PutAsJsonAsync(
                           $"{tenantBPath}/programs/{programA}/criteria-edition",
                           new { expected_revision = 1, edition_id = editionId }))
                    Assert.Equal(HttpStatusCode.NotFound, swapped.StatusCode);
                _ = await ownerMcp.When("bdgrz.program.criteria.select",
                    SelectInput(tenantB, programA, editionId)).ExpectFailure("NotFound");
                using (var swappedRead = await owner.GetAsync($"{tenantBPath}/programs/{programA}"))
                    Assert.Equal(HttpStatusCode.NotFound, swappedRead.StatusCode);
                _ = await ownerMcp.When("bdgrz.program.get", new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantB.ToString(),
                    ["program_id"] = programA,
                }).ExpectFailure("NotFound");

                // A catalog cursor issued in tenant A is invalid in tenant B.
                using (var transplanted = await owner.GetAsync(
                           $"{tenantBPath}/criteria-editions/{editionId}/entries?limit=5&cursor=" +
                           Uri.EscapeDataString(tenantACursor)))
                    Assert.Equal(HttpStatusCode.BadRequest, transplanted.StatusCode);
                _ = await ownerMcp.When("bdgrz.criteria.entries.list",
                    new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenantB.ToString(),
                        ["edition_id"] = editionId,
                        ["limit"] = 5,
                        ["cursor"] = tenantACursor,
                    }).ExpectFailure("Validation");

                // Assert: no denied or swapped probe wrote a selection anywhere.
                var store = (worker?.Services ?? factory.Services).GetRequiredService<IEventStore>();
                Assert.Equal(1, await CountSelectionsAsync(store, tenantA, programA));
                Assert.Equal(0, await CountSelectionsAsync(store, tenantB, programA));
                Assert.Equal(0, await CountSelectionsAsync(store, tenantB, programB));
                var programBView = await ReadJsonAsync(await owner.GetAsync(
                    $"{tenantBPath}/programs/{programB}"));
                Assert.Equal(JsonValueKind.Null,
                    programBView.GetProperty("criteria_edition_id").ValueKind);
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

    static IEnumerable<(string Tool, Dictionary<string, object?> Input)> CatalogProbes(
        Uuid tenantId, string editionId)
    {
        var tenant = tenantId.ToString();
        yield return ("bdgrz.criteria.editions.list",
            new Dictionary<string, object?> { ["tenant_id"] = tenant });
        yield return ("bdgrz.criteria.edition.get",
            new Dictionary<string, object?> { ["tenant_id"] = tenant, ["edition_id"] = editionId });
        yield return ("bdgrz.criteria.entries.list",
            new Dictionary<string, object?> { ["tenant_id"] = tenant, ["edition_id"] = editionId });
        yield return ("bdgrz.criteria.entry.get", new Dictionary<string, object?>
        {
            ["tenant_id"] = tenant,
            ["edition_id"] = editionId,
            ["identifier"] = "CC6.1",
        });
    }

    static Dictionary<string, object?> SelectInput(Uuid tenantId, string programId,
        string editionId) => new()
        {
            ["tenant_id"] = tenantId.ToString(),
            ["program_id"] = programId,
            ["expected_revision"] = 1,
            ["edition_id"] = editionId,
        };

    static async Task<int> CountSelectionsAsync(IEventStore store, Uuid tenantId, string programId)
    {
        var count = 0;
        await foreach (var record in store.ReadAsync(new ComplianceProgram(tenantId,
                           Uuid.Parse(programId, CultureInfo.InvariantCulture)).Stream, 0,
                           CancellationToken.None))
            if (record.Event is ProgramCriteriaEditionSelected)
                count++;
        return count;
    }

    static async Task<Uuid> CreateTenantAsync(HttpClient owner, string name)
    {
        using var response = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name,
            slug = $"criteria-matrix-{Guid.NewGuid():N}"[..24],
        });
        return Uuid.Parse((await ReadJsonAsync(response)).GetProperty("tenant_id").GetString()!,
            CultureInfo.InvariantCulture);
    }

    static async Task<string> CreateProgramAsync(HttpClient owner, Uuid tenantId)
    {
        var path = TenantPath(tenantId) + "/programs";
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(path, new
            {
                name = "Criteria matrix program",
                plan = new
                {
                    target_readiness_date = (string?)null,
                    target_type_i_as_of_date = (string?)null,
                    target_type_ii_start_date = (string?)null,
                    target_type_ii_end_date = (string?)null,
                    readiness_advisor = (string?)null,
                    audit_firm = (string?)null,
                },
            });
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var programId = (await ReadJsonAsync(response)).GetProperty("program_id")
                    .GetString()!;
                while (DateTimeOffset.UtcNow < deadline)
                {
                    using var read = await owner.GetAsync($"{path}/{programId}?minimum_revision=1");
                    if (read.StatusCode == HttpStatusCode.OK)
                        return programId;
                    await Task.Delay(250);
                }
            }
            Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound or
                HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("Program creation remained unauthorized after tenant bootstrap.");
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

    static string TenantPath(Uuid tenantId) => "/api/v1/tenants/" + tenantId;
}
