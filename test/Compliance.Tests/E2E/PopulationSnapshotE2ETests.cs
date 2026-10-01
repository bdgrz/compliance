using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     Broker proof for chunked population snapshots (EN-03b): a roster beyond the inline bound is
///     frozen across storage-chunk streams in split API/worker hosting, read and regenerated from
///     its source while the directory projection lags, and replays identically in a fresh host.
/// </summary>
[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class PopulationSnapshotE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    const int People = 640;

    [Fact]
    public async Task ShouldFreezeReadAndRegenerateChunkedRosterGivenSplitHostLagAndReplay()
    {
        // Arrange
        var applicationName = $"compliance-population-split-{Guid.NewGuid():N}";
        using var worker = BuildWorker(applicationName);
        await worker.StartAsync();
        await using var factory = E2EAppFactory.Create(broker, applicationName);
        using var owner = ApiClient(factory);
        using var outsider = ApiClient(factory);
        var ownerEmail = $"population-owner-{Guid.NewGuid():N}@example.com";
        await TenantInvitationE2ETests.LoginAsync(owner, ownerEmail);
        await TenantInvitationE2ETests.LoginAsync(outsider,
            $"population-outsider-{Guid.NewGuid():N}@example.com");
        var tenantId = await CreateTenantAsync(owner);
        var root = $"/api/v1/tenants/{tenantId}";
        _ = await RecordWhenAuthorizedAsync(owner, $"{root}/people",
            new { display_name = "Person 0000" });
        await Parallel.ForEachAsync(Enumerable.Range(1, People - 1),
            new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (index, ct) =>
            {
                using var response = await owner.PostAsJsonAsync($"{root}/people",
                    new { display_name = $"Person {index.ToString("D4", CultureInfo.InvariantCulture)}" }, ct);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            });

        // Act
        var registration = await FreezeWhenCaughtUpAsync(owner, $"{root}/workforce-roster-snapshots");
        var snapshotId = registration.GetProperty("snapshot_id").GetString();
        var snapshotPath = $"{root}/workforce-roster-snapshots/{snapshotId}";
        var frozen = await WaitForOkAsync(owner, snapshotPath);
        var first = await WaitForOkAsync(owner, $"{snapshotPath}/manifest-regeneration");
        await worker.StopAsync();
        var duringLag = await WaitForOkAsync(owner, snapshotPath);
        var regeneratedDuringLag = await WaitForOkAsync(owner, $"{snapshotPath}/manifest-regeneration");
        using var outsiderRead = await outsider.GetAsync(snapshotPath);
        using var outsiderRegenerate = await outsider.GetAsync($"{snapshotPath}/manifest-regeneration");
        using var restarted = BuildWorker(applicationName);
        await restarted.StartAsync();
        // The roster snapshot directory is not asserted here: it never projects in split-host
        // mode (#498), which predates chunking.
        await restarted.StopAsync();
        await using var replayFactory = E2EAppFactory.Create(broker, applicationName);
        using var replayClient = ApiClient(replayFactory);
        await TenantInvitationE2ETests.LoginAsync(replayClient, ownerEmail);
        var replayed = await WaitForOkAsync(replayClient, $"{snapshotPath}/manifest-regeneration");
        await using var mcp = await McpScenario.ConnectAsync(owner, new Uri(owner.BaseAddress!, "/mcp"));
        _ = await mcp.When("bdgrz.snapshot.workforce_roster.manifest_regenerate",
            new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["snapshot_id"] = snapshotId,
            }).ExpectSuccess();

        // Assert
        Assert.Equal(People, frozen.GetProperty("row_count").GetInt64());
        Assert.Equal(People, frozen.GetProperty("people").GetArrayLength());
        Assert.Equal(registration.GetProperty("content_sha256").GetString(),
            frozen.GetProperty("content_sha256").GetString());
        Assert.Equal(frozen.GetProperty("content_sha256").GetString(),
            duringLag.GetProperty("content_sha256").GetString());
        Assert.Equal(People, duringLag.GetProperty("people").GetArrayLength());
        Assert.Equal(first.GetProperty("manifest_sha256").GetString(),
            regeneratedDuringLag.GetProperty("manifest_sha256").GetString());
        Assert.Equal(first.GetProperty("canonical_manifest").GetString(),
            replayed.GetProperty("canonical_manifest").GetString());
        Assert.Equal(first.GetProperty("manifest_sha256").GetString(),
            replayed.GetProperty("manifest_sha256").GetString());
        Assert.Equal(People, first.GetProperty("row_count").GetInt64());
        Assert.Equal(HttpStatusCode.NotFound, outsiderRead.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderRegenerate.StatusCode);
        await using var scope = replayFactory.Services.CreateAsyncScope();
        var stored = await Bdgrz.Compliance.Features.Snapshots.PopulationSnapshotContent.ReadAsync(
            scope.ServiceProvider.GetRequiredService<Cntryl.Portia.IAggregateReader>(),
            Cntryl.Portia.Uuid.Parse(tenantId.ToString(), CultureInfo.InvariantCulture),
            Cntryl.Portia.Uuid.Parse(snapshotId!, CultureInfo.InvariantCulture), "workforce_roster",
            CancellationToken.None);
        Assert.True(stored.IsSuccess);
        Assert.True(stored.Value.Snapshot.StoredChunkCount > 1,
            "A roster beyond the inline bound must be stored in several chunks.");
    }

    static HttpClient ApiClient(WebApplicationFactory<Program> factory)
    {
        var priorMode = TestHostMode.Current;
        try
        {
            TestHostMode.Set("api");
            return factory.CreateClient();
        }
        finally
        {
            TestHostMode.Set(priorMode);
        }
    }

    static async Task<Guid> CreateTenantAsync(HttpClient owner)
    {
        using var tenant = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Population tenant",
            slug = $"population-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, tenant.StatusCode);
        return Guid.Parse((await ReadAsync(tenant)).GetProperty("tenant_id").GetString()!);
    }

    static async Task<string> RecordWhenAuthorizedAsync(HttpClient owner, string path, object body)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(path, body);
            if (response.StatusCode == HttpStatusCode.OK)
                return (await ReadAsync(response)).GetProperty("person_id").GetString()!;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("Recording a person never became authorized after tenant bootstrap.");
    }

    /// <summary>Retries the freeze while the roster projections catch up to their sources.</summary>
    static async Task<JsonElement> FreezeWhenCaughtUpAsync(HttpClient owner, string path)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(path, new { });
            if (response.StatusCode == HttpStatusCode.OK)
                return await ReadAsync(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await Task.Delay(250);
        }
        throw new TimeoutException("The roster freeze never caught up with its projections.");
    }

    static Task<JsonElement> WaitForOkAsync(HttpClient client, string path) =>
        WaitForAsync(client, path, static _ => true);

    static async Task<JsonElement> WaitForAsync(HttpClient client, string path,
        Func<JsonElement, bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var body = await ReadAsync(response);
                if (condition(body))
                    return body;
            }
            else
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await Task.Delay(250);
        }
        throw new TimeoutException($"{path} did not reach the expected state.");
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

    static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
        .RootElement.Clone();
}
