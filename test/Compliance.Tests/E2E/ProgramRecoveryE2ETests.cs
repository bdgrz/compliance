using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ProgramRecoveryE2ETests(RestartableBrokerStackFixture broker)
    : IClassFixture<RestartableBrokerStackFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRestoreProgramHistoryAndReplaySourceGivenFreshBrokerContainer(
        bool splitHosts)
    {
        // Arrange
        var applicationName = $"compliance-program-recovery-{Guid.NewGuid():N}";
        var email = $"program-recovery-{Guid.NewGuid():N}@example.com";
        IHost? sourceWorker = null;
        IHost? restoredWorker = null;
        WebApplicationFactory<Program>? sourceFactory = null;
        WebApplicationFactory<Program>? restoredFactory = null;
        HttpClient? sourceClient = null;
        HttpClient? restoredClient = null;

        try
        {
            if (splitHosts)
            {
                sourceWorker = BuildApplicationWorker(broker.WebSocketEndpoint, applicationName);
                await sourceWorker.StartAsync();
            }

            sourceFactory = E2EAppFactory.Create(broker.WebSocketEndpoint, applicationName);
            sourceClient = CreateClient(sourceFactory, splitHosts);
            await TenantInvitationE2ETests.LoginAsync(sourceClient, email);
            var tenantId = await CreateTenantAsync(sourceClient, "Recovery source tenant");
            var secondTenantId = await CreateTenantAsync(sourceClient, "Recovery other tenant");
            var (programId, programPath) = await CreateProgramAsync(sourceClient, tenantId);
            await ReviseProgramAsync(sourceClient, programPath);
            var sourceCurrent = await WaitForProgramAsync(sourceClient, programPath, 2);
            var sourceHistory = await WaitForHistoryAsync(sourceClient, programPath, 2);
            var sourceSnapshot = Snapshot(sourceCurrent, sourceHistory);
            var sourceEvents = await ReadProgramEventsAsync(sourceFactory, tenantId, programId);
            Assert.Collection(sourceEvents,
                static record => Assert.IsType<ProgramCreated>(record.Event),
                static record => Assert.IsType<ProgramRevised>(record.Event));

            // Act
            sourceClient.Dispose();
            sourceClient = null;
            await sourceFactory.DisposeAsync();
            sourceFactory = null;
            await StopAndDisposeAsync(sourceWorker);
            sourceWorker = null;
            await broker.RestartAsync();

            if (splitHosts)
            {
                restoredWorker = BuildApplicationWorker(broker.WebSocketEndpoint, applicationName);
                await restoredWorker.StartAsync();
            }

            restoredFactory = E2EAppFactory.Create(broker.WebSocketEndpoint, applicationName);
            restoredClient = CreateClient(restoredFactory, splitHosts);
            await TenantInvitationE2ETests.LoginAsync(restoredClient, email);

            // Assert
            var restoredCurrent = await WaitForProgramAsync(restoredClient, programPath, 2);
            var restoredHistory = await WaitForHistoryAsync(restoredClient, programPath, 2);
            Assert.Equal(sourceCurrent.ToJsonString(), restoredCurrent.ToJsonString());
            Assert.Equal(sourceHistory.ToJsonString(), restoredHistory.ToJsonString());
            var restoredEvents = await ReadProgramEventsAsync(restoredFactory, tenantId, programId);
            Assert.Equal(sourceEvents.Count, restoredEvents.Count);
            Assert.Collection(restoredEvents,
                static record => Assert.IsType<ProgramCreated>(record.Event),
                static record => Assert.IsType<ProgramRevised>(record.Event));

            using var undisclosed = await restoredClient.GetAsync(
                $"/api/v1/tenants/{secondTenantId}/programs/{programId}");
            Assert.Equal(HttpStatusCode.NotFound, undisclosed.StatusCode);

            await AssertReplayedProjectionAsync(restoredFactory.Services, tenantId, programId,
                sourceSnapshot);
        }
        finally
        {
            restoredClient?.Dispose();
            if (restoredFactory is not null)
                await restoredFactory.DisposeAsync();
            await StopAndDisposeAsync(restoredWorker);
            sourceClient?.Dispose();
            if (sourceFactory is not null)
                await sourceFactory.DisposeAsync();
            await StopAndDisposeAsync(sourceWorker);
        }
    }

    static IHost BuildApplicationWorker(string endpoint, string applicationName)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Configuration["Fitz:Endpoint"] = endpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
        return builder.Build();
    }

    static async Task<string> CreateTenantAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/tenants", new
        {
            name,
            slug = $"recovery-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadObjectAsync(response);
        return Assert.IsType<string>(body["tenant_id"]?.GetValue<string>());
    }

    static async Task<(string ProgramId, string Path)> CreateProgramAsync(HttpClient client,
        string tenantId)
    {
        var path = $"/api/v1/tenants/{tenantId}/programs";
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.PostAsJsonAsync(path, new
            {
                name = "Recovery program",
                plan = Plan("Recovery source advisor"),
            });
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var body = await ReadObjectAsync(response);
                var programId = Assert.IsType<string>(body["program_id"]?.GetValue<string>());
                return (programId, $"{path}/{programId}");
            }

            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }

        throw new TimeoutException("The program was not created before authorization projected.");
    }

    static async Task ReviseProgramAsync(HttpClient client, string path)
    {
        using var response = await client.PutAsJsonAsync(path, new
        {
            expected_revision = 1,
            name = "Recovered program",
            plan = Plan("Recovery restored advisor"),
        });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    static async Task<JsonObject> WaitForProgramAsync(HttpClient client, string path, long revision)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var body = await ReadObjectAsync(response);
                if (body["revision"]?.GetValue<long>() == revision)
                    return body;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"The program projection did not reach revision {revision}.");
    }

    static async Task<JsonObject> WaitForHistoryAsync(HttpClient client, string path, int count)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync($"{path}/revisions");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var body = await ReadObjectAsync(response);
                if (body["items"] is JsonArray { Count: var actual } && actual == count)
                    return body;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"The program history did not reach {count} revisions.");
    }

    static async Task<IReadOnlyList<DomainEventRecord>> ReadProgramEventsAsync(
        WebApplicationFactory<Program> factory, string tenantId, string programId)
    {
        var events = new List<DomainEventRecord>();
        var store = factory.Services.GetRequiredService<IEventStore>();
        await foreach (var record in store.ReadAsync(
                           new EventStreamAddress(tenantId, "programs", programId), 0,
                           CancellationToken.None))
        {
            events.Add(record);
        }

        return events;
    }

    static async Task AssertReplayedProjectionAsync(IServiceProvider sourceServices, string tenantId,
        string programId, ProgramProjectionSnapshot expected)
    {
        var tenant = Uuid.Parse(tenantId, CultureInfo.InvariantCulture);
        var program = Uuid.Parse(programId, CultureInfo.InvariantCulture);
        using var replay = BuildReplayWorker(
            sourceServices.GetRequiredService<IEventStore>(), tenant);
        await replay.StartAsync();
        try
        {
            var (current, revisions) = await WaitForReplayedProgramAsync(replay.Services, tenant,
                program, expected.Revisions.Count);
            Assert.Equal(expected.Name, current.Name);
            Assert.Equal(expected.Revision, current.Revision);
            Assert.Equal(expected.Plan, Snapshot(current.Plan));
            Assert.Equal(expected.Revisions,
                revisions.Select(static revision => new ProgramRevisionSnapshot(revision.Revision,
                    revision.Name, Snapshot(revision.Plan))).ToArray());
        }
        finally
        {
            await replay.StopAsync();
        }
    }

    static IHost BuildReplayWorker(IDomainEventReader reader, Uuid tenant)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Services.AddSingleton(reader);
        builder.Services.AddSingleton<IKvClient, InMemoryKvClient>();
        builder.Services.AddSingleton<ITenantDirectory>(new StaticTenantDirectory(
            new TenantId(tenant.ToString())));
        builder.Services.AddScoped<FitzProgramDirectory>();
        builder.Services.AddScoped<IProgramDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzProgramDirectory>());
        builder.Services.AddScoped<IProgramDirectoryReader>(provider =>
            provider.GetRequiredService<FitzProgramDirectory>());
        builder.Services.AddPortiaEvent<ProgramCreated>(1, "bdgrz.program.created");
        builder.Services.AddPortiaEvent<ProgramRevised>(1, "bdgrz.program.revised");
        builder.Services.AddPortia()
            .AddProjector<ProgramDirectoryProjector>("ProgramDirectory", WorkloadScope.PerTenant,
                options => options.PollInterval = TimeSpan.FromMilliseconds(10))
            .AddWorkers();
        return builder.Build();
    }

    static async Task<(ProgramView Current, IReadOnlyList<ProgramRevisionView> Revisions)>
        WaitForReplayedProgramAsync(IServiceProvider services, Uuid tenantId, Uuid programId,
            int expectedRevisionCount)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var scope = services.CreateScope();
            var directory = scope.ServiceProvider.GetRequiredService<IProgramDirectoryReader>();
            var current = await directory.GetAsync(tenantId, programId);
            var history = await directory.ListRevisionsAsync(tenantId, programId, 200, null);
            if (current is not null && history?.Items.Count == expectedRevisionCount)
                return (current, history.Items);

            await Task.Delay(250);
        }

        throw new TimeoutException("The isolated replay projection did not reach the restored source.");
    }

    static HttpClient CreateClient(WebApplicationFactory<Program> factory, bool splitHosts)
    {
        if (!splitHosts)
            return factory.CreateClient();

        var priorMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
        try
        {
            Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", "api");
            return factory.CreateClient();
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", priorMode);
        }
    }

    static async Task StopAndDisposeAsync(IHost? worker)
    {
        if (worker is null)
            return;

        await worker.StopAsync();
        worker.Dispose();
    }

    static async Task<JsonObject> ReadObjectAsync(HttpResponseMessage response) => Assert.IsType<JsonObject>(
        JsonNode.Parse(await response.Content.ReadAsStringAsync()));

    static object Plan(string readinessAdvisor) => new
    {
        target_readiness_date = "2027-01-31",
        target_type_i_as_of_date = "2027-03-31",
        target_type_ii_start_date = "2027-04-01",
        target_type_ii_end_date = "2028-03-31",
        readiness_advisor = readinessAdvisor,
        audit_firm = "Recovery audit firm",
    };

    static ProgramProjectionSnapshot Snapshot(JsonObject current, JsonObject history) => new(
        Assert.IsType<string>(current["name"]?.GetValue<string>()),
        Assert.IsType<long>(current["revision"]?.GetValue<long>()),
        Snapshot(current["plan"]?.AsObject()),
        history["items"]?.AsArray().Select(static item => new ProgramRevisionSnapshot(
            Assert.IsType<long>(item?["revision"]?.GetValue<long>()),
            Assert.IsType<string>(item?["name"]?.GetValue<string>()),
            Snapshot(item?["plan"]?.AsObject()))).ToArray() ?? []);

    static ProgramPlanSnapshot Snapshot(ProgramPlan plan) => new(
        Date(plan.TargetReadinessDate),
        Date(plan.TargetTypeIAsOfDate),
        Date(plan.TargetTypeIIStartDate),
        Date(plan.TargetTypeIIEndDate),
        plan.ReadinessAdvisor,
        plan.AuditFirm);

    static ProgramPlanSnapshot Snapshot(JsonObject? plan) => new(
        Text(plan?["target_readiness_date"]),
        Text(plan?["target_type_i_as_of_date"]),
        Text(plan?["target_type_ii_start_date"]),
        Text(plan?["target_type_ii_end_date"]),
        Text(plan?["readiness_advisor"]),
        Text(plan?["audit_firm"]));

    static string? Date(DateOnly? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    static string? Text(JsonNode? value) => value?.GetValue<string>();

    sealed class StaticTenantDirectory(TenantId tenant) : ITenantDirectory
    {
        public async IAsyncEnumerable<TenantId> GetActiveTenantsAsync(
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            yield return tenant;
            await Task.CompletedTask;
        }

        public async IAsyncEnumerable<TenantLifecycleChange> WatchAsync(
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            yield break;
        }
    }

    sealed record ProgramProjectionSnapshot(string Name, long Revision, ProgramPlanSnapshot Plan,
        IReadOnlyList<ProgramRevisionSnapshot> Revisions);

    sealed record ProgramRevisionSnapshot(long Revision, string Name, ProgramPlanSnapshot Plan);

    sealed record ProgramPlanSnapshot(string? TargetReadinessDate, string? TargetTypeIAsOfDate,
        string? TargetTypeIIStartDate, string? TargetTypeIIEndDate, string? ReadinessAdvisor,
        string? AuditFirm);
}
