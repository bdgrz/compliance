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
public sealed class ApplicationImportE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldStageAndPreviewWithoutApplicationEventsGivenStandaloneHost()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var owner = factory.CreateClient();
        using var outsider = factory.CreateClient();
        await TenantInvitationE2ETests.LoginAsync(owner,
            $"import-owner-{Guid.NewGuid():N}@example.com");
        await TenantInvitationE2ETests.LoginAsync(outsider,
            $"import-outsider-{Guid.NewGuid():N}@example.com");
        var tenantId = await CreateTenantAsync(owner);
        var path = $"/api/v1/tenants/{tenantId}/application_imports";
        var submissionId = Guid.NewGuid();
        var rows = Enumerable.Range(1, 200).Select(number => new
        {
            source_record_id = number == 2 ? "record-1" : number == 3 ? " " : $"record-{number}",
            name = number == 2 ? " " : $"Application {number}",
            purpose = "Inventory observation",
            owner_reference = (string?)null,
        }).ToArray();
        var body = new
        {
            submission_id = submissionId,
            source_key = "tenant_list",
            source_namespace = "primary",
            coverage = "partial",
            rows,
        };

        // Act
        var first = await StageWhenAuthorizedAsync(owner, path, body);
        using var replayResponse = await owner.PostAsJsonAsync(path, body);
        using var bodyTenantResponse = await owner.PostAsJsonAsync(path, new
        {
            tenant_id = Guid.NewGuid(),
            body.submission_id,
            body.source_key,
            body.source_namespace,
            body.coverage,
            body.rows,
        });
        using var changedResponse = await owner.PostAsJsonAsync(path, new
        {
            body.submission_id,
            body.source_key,
            body.source_namespace,
            body.coverage,
            rows = new[] { new { source_record_id = "record-1", name = "Changed",
                purpose = "Changed", owner_reference = (string?)null } },
        });
        var replay = await ReadAsync(replayResponse);
        var batchPath = $"{path}/{first.GetProperty("batch_id").GetString()}";
        var projected = await WaitForBatchAsync(owner, batchPath);
        using var futureRevision = await owner.GetAsync($"{batchPath}?minimum_revision=2");
        using var rowsResponse = await owner.GetAsync($"{batchPath}/rows?limit=137");
        var firstPage = await ReadAsync(rowsResponse);
        var cursor = firstPage.GetProperty("next_cursor").GetString();
        using var nextResponse = await owner.GetAsync(
            $"{batchPath}/rows?limit=137&cursor={Uri.EscapeDataString(cursor!)}");
        var nextPage = await ReadAsync(nextResponse);
        using var invalidCursor = await owner.GetAsync($"{batchPath}/rows?cursor=not-a-cursor");
        using var invalidLimit = await owner.GetAsync($"{batchPath}/preview?limit=201");
        using var secondStageResponse = await owner.PostAsJsonAsync(path, Body(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, secondStageResponse.StatusCode);
        var secondStage = await ReadAsync(secondStageResponse);
        var secondBatchPath = $"{path}/{secondStage.GetProperty("batch_id").GetString()}";
        _ = await WaitForBatchAsync(owner, secondBatchPath);
        using var crossBatchCursor = await owner.GetAsync(
            $"{secondBatchPath}/rows?cursor={Uri.EscapeDataString(cursor!)}");
        using var oversized = await owner.PostAsJsonAsync(path, new
        {
            submission_id = Guid.NewGuid(),
            source_key = "tenant_list",
            source_namespace = "primary",
            coverage = "partial",
            rows = Enumerable.Range(1, 200).Select(number => new
            {
                source_record_id = $"large-{number}",
                name = "Application",
                purpose = new string('p', 2000),
                owner_reference = (string?)null,
            }).ToArray(),
        });
        using var previewResponse = await owner.GetAsync($"{batchPath}/preview?limit=3");
        var preview = await ReadAsync(previewResponse);
        using var deniedBatch = await outsider.GetAsync(batchPath);
        using var deniedRows = await outsider.GetAsync($"{batchPath}/rows");
        using var deniedPreview = await outsider.GetAsync($"{batchPath}/preview");
        using var deniedStage = await outsider.PostAsJsonAsync(path, Body(Guid.NewGuid()));
        using var alienBatch = await owner.GetAsync(
            $"/api/v1/tenants/{Guid.NewGuid()}/application_imports/{first.GetProperty("batch_id").GetString()}");
        var secondTenantId = await CreateTenantAsync(owner);
        var secondTenantPath = $"/api/v1/tenants/{secondTenantId}/application_imports";
        _ = await StageWhenAuthorizedAsync(owner, secondTenantPath, Body(Guid.NewGuid()));
        using var crossTenantBatch = await owner.GetAsync(
            $"{secondTenantPath}/{first.GetProperty("batch_id").GetString()}");
        using var crossTenantRows = await owner.GetAsync(
            $"{secondTenantPath}/{first.GetProperty("batch_id").GetString()}/rows");
        using var crossTenantPreview = await owner.GetAsync(
            $"{secondTenantPath}/{first.GetProperty("batch_id").GetString()}/preview");
        using var applications = await owner.GetAsync($"/api/v1/tenants/{tenantId}/applications");
        var applicationsBody = await ReadAsync(applications);
        var applicationEvents = new List<DomainEvent>();
        var store = factory.Services.GetRequiredService<IEventStore>();
        await foreach (var record in store.ReadAsync(
                           EventStreamPattern.ForPattern(tenantId.ToString(), "applications"),
                           EventCursor.Start, CancellationToken.None))
            applicationEvents.Add(record.Event);
        var importEvents = new List<DomainEvent>();
        await foreach (var record in store.ReadAsync(
                           EventStreamPattern.ForPattern(tenantId.ToString(), "application_imports"),
                           EventCursor.Start, CancellationToken.None))
            importEvents.Add(record.Event);

        // Assert
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, bodyTenantResponse.StatusCode);
        Assert.Equal(first.GetProperty("batch_id").GetString(),
            replay.GetProperty("batch_id").GetString());
        Assert.Equal(HttpStatusCode.Conflict, changedResponse.StatusCode);
        Assert.Equal(200, projected.GetProperty("row_count").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, futureRevision.StatusCode);
        Assert.Equal("true", futureRevision.Headers.GetValues("Portia-Transient").Single());
        Assert.Equal(3, projected.GetProperty("invalid_count").GetInt32());
        Assert.Equal(137, firstPage.GetProperty("items").GetArrayLength());
        Assert.Equal(63, nextPage.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.BadRequest, invalidCursor.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidLimit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, crossBatchCursor.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
        Assert.Equal("duplicate", preview.GetProperty("items")[0]
            .GetProperty("match_state").GetString());
        Assert.Contains("source_claims_unavailable", preview.GetProperty("items")[0]
            .GetProperty("acceptance_blockers").EnumerateArray()
            .Select(item => item.GetString()));
        Assert.Equal(HttpStatusCode.NotFound, deniedBatch.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedRows.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedPreview.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedStage.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, alienBatch.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantBatch.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantRows.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantPreview.StatusCode);
        Assert.Empty(applicationsBody.GetProperty("items").EnumerateArray());
        Assert.Empty(applicationEvents);
        Assert.Equal(2, importEvents.Count);
        await using (var mcp = await McpScenario.ConnectAsync(owner,
                         new Uri(owner.BaseAddress!, "/mcp")))
        {
            var arguments = new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["batch_id"] = first.GetProperty("batch_id").GetString(),
            };
            _ = await mcp.When("bdgrz.application_import.get", arguments).ExpectSuccess();
            _ = await mcp.When("bdgrz.application_import.rows.list", arguments).ExpectSuccess();
            _ = await mcp.When("bdgrz.application_import.preview", arguments).ExpectSuccess();
        }
        await using (var mcp = await McpScenario.ConnectAsync(outsider,
                         new Uri(outsider.BaseAddress!, "/mcp")))
        {
            _ = await mcp.When("bdgrz.application_import.get", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["batch_id"] = first.GetProperty("batch_id").GetString(),
            }).ExpectFailure();
            _ = await mcp.When("bdgrz.application_import.rows.list", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["batch_id"] = first.GetProperty("batch_id").GetString(),
            }).ExpectFailure();
            _ = await mcp.When("bdgrz.application_import.preview", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["batch_id"] = first.GetProperty("batch_id").GetString(),
            }).ExpectFailure();
        }
    }

    [Fact]
    public async Task ShouldReplayPreexistingImportGivenSplitWorkerRestart()
    {
        // Arrange
        var applicationName = $"compliance-split-import-{Guid.NewGuid():N}";
        using var worker = BuildWorker(applicationName);
        await worker.StartAsync();
        await using var factory = E2EAppFactory.Create(broker, applicationName);
        var previousMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
        HttpClient client;
        try
        {
            Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", "api");
            client = factory.CreateClient();
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", previousMode);
        }
        using var owner = client;
        await TenantInvitationE2ETests.LoginAsync(owner,
            $"split-import-owner-{Guid.NewGuid():N}@example.com");
        var tenantId = await CreateTenantAsync(owner);
        var path = $"/api/v1/tenants/{tenantId}/application_imports";
        var first = await StageWhenAuthorizedAsync(owner, path, Body(Guid.NewGuid()));
        _ = await WaitForBatchAsync(owner,
            $"{path}/{first.GetProperty("batch_id").GetString()}");
        await worker.StopAsync();

        // Act
        using var stageResponse = await owner.PostAsJsonAsync(path, Body(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, stageResponse.StatusCode);
        var staged = await ReadAsync(stageResponse);
        var batchPath = $"{path}/{staged.GetProperty("batch_id").GetString()}";
        using var lagged = await owner.GetAsync($"{batchPath}/preview?minimum_revision=1");
        using var restarted = BuildWorker(applicationName);
        await restarted.StartAsync();
        try
        {
            var projected = await WaitForBatchAsync(owner, batchPath);
            using var previewResponse = await owner.GetAsync($"{batchPath}/preview");
            var preview = await ReadAsync(previewResponse);

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, lagged.StatusCode);
            Assert.Equal("true", lagged.Headers.GetValues("Portia-Transient").Single());
            Assert.Equal(1, projected.GetProperty("row_count").GetInt32());
            Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
            Assert.Single(preview.GetProperty("items").EnumerateArray());
            var store = factory.Services.GetRequiredService<IEventStore>();
            var applicationEvents = new List<DomainEvent>();
            await foreach (var record in store.ReadAsync(
                               EventStreamPattern.ForPattern(tenantId.ToString(), "applications"),
                               EventCursor.Start, CancellationToken.None))
                applicationEvents.Add(record.Event);
            Assert.Empty(applicationEvents);
        }
        finally
        {
            await restarted.StopAsync();
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

    static object Body(Guid submissionId) => new
    {
        submission_id = submissionId,
        source_key = "tenant_list",
        source_namespace = "primary",
        coverage = "partial",
        rows = new[] { new { source_record_id = "record-1", name = "Payroll",
            purpose = "Inventory observation", owner_reference = (string?)null } },
    };

    static async Task<Guid> CreateTenantAsync(HttpClient owner)
    {
        using var response = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Application import tenant",
            slug = $"import-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Guid.Parse((await ReadAsync(response)).GetProperty("tenant_id").GetString()!);
    }

    static async Task<JsonElement> StageWhenAuthorizedAsync(HttpClient owner,
        string path, object body)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(path, body);
            if (response.StatusCode == HttpStatusCode.OK)
                return await ReadAsync(response);
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("Import staging never became authorized after tenant bootstrap.");
    }

    static async Task<JsonElement> WaitForBatchAsync(HttpClient owner, string batchPath)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(batchPath);
            if (response.StatusCode == HttpStatusCode.OK)
                return await ReadAsync(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await Task.Delay(250);
        }
        throw new TimeoutException("Import projection did not catch up with the staged source.");
    }

    static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync())).RootElement.Clone();
}
