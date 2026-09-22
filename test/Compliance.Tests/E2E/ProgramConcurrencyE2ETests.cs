using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ProgramConcurrencyE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReturnTransientConflictGivenTwoConcurrentProgramRevisionsThroughHttp(
        bool splitHosts)
    {
        // Arrange
        var gate = new ConcurrentProgramRevisionGate();
        var applicationName = $"compliance-program-concurrency-http-{Guid.NewGuid():N}";
        IHost? worker = null;
        if (splitHosts)
        {
            worker = BuildWorker(applicationName);
            await worker.StartAsync();
        }

        try
        {
            await using var factory = CreateFactory(applicationName, gate);
            using var firstClient = CreateClient(factory, splitHosts);
            using var secondClient = CreateClient(factory, splitHosts);
            var email = $"program-concurrency-{Guid.NewGuid():N}@example.com";
            await TenantInvitationE2ETests.LoginAsync(firstClient, email);
            await TenantInvitationE2ETests.LoginAsync(secondClient, email);
            var (tenantId, programId, path) = await CreateProgramAsync(firstClient);
            await WaitForRevisionAsync(firstClient, path, 1);
            gate.Arm();

            // Act
            var first = firstClient.PutAsJsonAsync(path, RevisionRequest("Concurrent revision one"));
            var second = secondClient.PutAsJsonAsync(path, RevisionRequest("Concurrent revision two"));
            using var firstResponse = await first;
            using var secondResponse = await second;
            var responses = new[] { firstResponse, secondResponse };
            var winner = Assert.Single(responses,
                response => response.StatusCode == HttpStatusCode.NoContent);
            var conflict = Assert.Single(responses,
                response => response.StatusCode == HttpStatusCode.Conflict);
            var conflictBody = await conflict.Content.ReadAsStringAsync();
            using var problem = JsonDocument.Parse(conflictBody);
            var detail = Assert.IsType<string>(problem.RootElement.GetProperty("detail").GetString());
            await WaitForRevisionAsync(firstClient, path, 2);
            var revisedEvents = await ReadRevisionsAsync(factory, tenantId, programId);

            // Assert
            var contention = Assert.IsType<EventStreamConcurrencyException>(gate.SaveFailure);
            var fitz = Assert.IsType<Cntryl.Fitz.StreamException>(contention.InnerException);
            Assert.Equal(Cntryl.Fitz.FitzErrorCodes.StreamSessionAlreadyActive, fitz.DomainCode);
            Assert.Equal(HttpStatusCode.NoContent, winner.StatusCode);
            Assert.Equal("true", conflict.Headers.GetValues("Portia-Transient").Single());
            Assert.Equal("The request conflicted with a concurrent update.", detail);
            Assert.Equal(path, problem.RootElement.GetProperty("instance").GetString());
            Assert.DoesNotContain("Fitz", detail, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("stream", detail, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(tenantId, detail, StringComparison.Ordinal);
            Assert.DoesNotContain(programId, detail, StringComparison.Ordinal);
            Assert.Equal(2, gate.DelegatedSaves);
            Assert.Single(revisedEvents);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReturnTransientConflictGivenTwoConcurrentProgramRevisionsThroughMcp(
        bool splitHosts)
    {
        // Arrange
        var gate = new ConcurrentProgramRevisionGate();
        var applicationName = $"compliance-program-concurrency-mcp-{Guid.NewGuid():N}";
        IHost? worker = null;
        if (splitHosts)
        {
            worker = BuildWorker(applicationName);
            await worker.StartAsync();
        }

        try
        {
            await using var factory = CreateFactory(applicationName, gate);
            using var firstClient = CreateClient(factory, splitHosts);
            using var secondClient = CreateClient(factory, splitHosts);
            var email = $"program-concurrency-mcp-{Guid.NewGuid():N}@example.com";
            await TenantInvitationE2ETests.LoginAsync(firstClient, email);
            await TenantInvitationE2ETests.LoginAsync(secondClient, email);
            var (tenantId, programId, path) = await CreateProgramAsync(firstClient);
            await WaitForRevisionAsync(firstClient, path, 1);
            await using var firstMcp = await McpScenario.ConnectAsync(firstClient,
                new Uri(firstClient.BaseAddress!, "/mcp"));
            await using var secondMcp = await McpScenario.ConnectAsync(secondClient,
                new Uri(secondClient.BaseAddress!, "/mcp"));
            gate.Arm();

            // Act
            var first = InvokeRevisionAsync(firstMcp, tenantId, programId,
                "Concurrent MCP revision one");
            var second = InvokeRevisionAsync(secondMcp, tenantId, programId,
                "Concurrent MCP revision two");
            var results = await Task.WhenAll(first, second);
            var winner = Assert.Single(results, result => !result.IsError);
            var conflict = Assert.Single(results, result => result.IsError);
            var structured = Assert.IsType<JsonElement>(conflict.StructuredJson);
            var conflictBody = string.Join("\n", conflict.Text);
            await WaitForRevisionAsync(firstClient, path, 2);
            var revisedEvents = await ReadRevisionsAsync(factory, tenantId, programId);

            // Assert
            var contention = Assert.IsType<EventStreamConcurrencyException>(gate.SaveFailure);
            var fitz = Assert.IsType<Cntryl.Fitz.StreamException>(contention.InnerException);
            Assert.Equal(Cntryl.Fitz.FitzErrorCodes.StreamSessionAlreadyActive, fitz.DomainCode);
            Assert.False(winner.IsError);
            Assert.Equal("Conflict", structured.GetProperty("kind").GetString());
            Assert.True(structured.GetProperty("isTransient").GetBoolean());
            Assert.Equal("The request conflicted with a concurrent update.",
                structured.GetProperty("message").GetString());
            Assert.Contains("The request conflicted with a concurrent update.", conflictBody,
                StringComparison.Ordinal);
            Assert.DoesNotContain("Fitz", conflictBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("stream", conflictBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(tenantId, conflictBody, StringComparison.Ordinal);
            Assert.DoesNotContain(programId, conflictBody, StringComparison.Ordinal);
            Assert.Equal(2, gate.DelegatedSaves);
            Assert.Single(revisedEvents);
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

    WebApplicationFactory<Program> CreateFactory(string applicationName,
        ConcurrentProgramRevisionGate gate) => E2EAppFactory.Create(broker, applicationName)
        .WithWebHostBuilder(host => host.ConfigureTestServices(services =>
        {
            var originalWriter = services.Single(descriptor =>
                descriptor.ServiceType == typeof(IAggregateWriter));
            var originalFactory = originalWriter.ImplementationFactory
                ?? throw new InvalidOperationException("Portia must register an aggregate writer factory.");
            services.Remove(originalWriter);
            services.AddSingleton(gate);
            services.AddScoped<IAggregateWriter>(provider => new CoordinatingProgramAggregateWriter(
                (IAggregateWriter)originalFactory(provider),
                provider.GetRequiredService<ConcurrentProgramRevisionGate>()));
        }));

    IHost BuildWorker(string applicationName)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
        return builder.Build();
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

    static async Task<(string TenantId, string ProgramId, string Path)> CreateProgramAsync(
        HttpClient client)
    {
        using var tenantResponse = await client.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Concurrent revision tenant",
            slug = $"concurrent-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, tenantResponse.StatusCode);
        var tenant = await tenantResponse.Content.ReadFromJsonAsync<TenantRegistrationDocument>();
        Assert.NotNull(tenant);
        var programs = $"/api/v1/tenants/{tenant.TenantId}/programs";
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.PostAsJsonAsync(programs, new
            {
                name = "Concurrent revision program",
                plan = Plan(),
            });
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var program = await response.Content.ReadFromJsonAsync<ProgramRegistrationDocument>();
                Assert.NotNull(program);
                return (tenant.TenantId, program.ProgramId, $"{programs}/{program.ProgramId}");
            }
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("The program was not created before the authorization projection caught up.");
    }

    static async Task WaitForRevisionAsync(HttpClient client, string path, long revision)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var program = await response.Content.ReadFromJsonAsync<ProgramDocument>();
                if (program?.Revision == revision)
                    return;
            }
            await Task.Delay(250);
        }
        throw new TimeoutException($"The program projection did not reach revision {revision}.");
    }

    static async Task<IReadOnlyList<ProgramRevised>> ReadRevisionsAsync(
        WebApplicationFactory<Program> factory, string tenantId, string programId)
    {
        var revised = new List<ProgramRevised>();
        var store = factory.Services.GetRequiredService<IEventStore>();
        await foreach (var record in store.ReadAsync(
                           EventStreamPattern.ForPattern(tenantId, "programs", programId),
                           EventCursor.Start, CancellationToken.None))
        {
            if (record.Event is ProgramRevised programRevised)
                revised.Add(programRevised);
        }
        return revised;
    }

    static async Task<McpCallSnapshot> InvokeRevisionAsync(McpScenario scenario,
        string tenantId, string programId, string name) => await scenario.When("bdgrz.program.revise",
        new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId,
            ["program_id"] = programId,
            ["expected_revision"] = 1,
            ["name"] = name,
            ["plan"] = Plan(),
        });

    static object RevisionRequest(string name) => new
    {
        expected_revision = 1,
        name,
        plan = Plan(),
    };

    static object Plan() => new
    {
        target_readiness_date = "2027-01-31",
        target_type_i_as_of_date = "2027-03-31",
        target_type_ii_start_date = "2027-04-01",
        target_type_ii_end_date = "2028-03-31",
        readiness_advisor = "Concurrent editor",
        audit_firm = (string?)null,
    };

    sealed class CoordinatingProgramAggregateWriter(IAggregateWriter inner,
        ConcurrentProgramRevisionGate gate) : IAggregateWriter
    {
        public async ValueTask SaveAsync<TAggregate>(TAggregate aggregate,
            IExecutionContext context, CancellationToken ct = default)
            where TAggregate : Aggregate
        {
            if (aggregate is ComplianceProgram && gate.TryClaim())
            {
                await gate.WaitForPairAsync(ct);
                gate.RecordDelegatedSave();
            }
            try
            {
                await inner.SaveAsync(aggregate, context, ct);
            }
            catch (Exception exception)
            {
                gate.RecordSaveFailure(exception);
                throw;
            }
        }
    }

    sealed class ConcurrentProgramRevisionGate
    {
        readonly TaskCompletionSource _bothWritesReady = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int _armed;
        int _claimed;
        int _delegatedSaves;
        Exception? _saveFailure;

        public int DelegatedSaves => Volatile.Read(ref _delegatedSaves);
        public Exception? SaveFailure => Volatile.Read(ref _saveFailure);

        public void Arm()
        {
            if (Interlocked.Exchange(ref _armed, 1) != 0)
                throw new InvalidOperationException("The concurrency gate can only be armed once.");
        }

        public bool TryClaim()
        {
            if (Volatile.Read(ref _armed) == 0)
                return false;
            return Interlocked.Increment(ref _claimed) <= 2;
        }

        public async ValueTask WaitForPairAsync(CancellationToken ct)
        {
            if (Volatile.Read(ref _claimed) == 2)
                _bothWritesReady.TrySetResult();
            await _bothWritesReady.Task.WaitAsync(ct);
        }

        public void RecordDelegatedSave() => Interlocked.Increment(ref _delegatedSaves);

        public void RecordSaveFailure(Exception exception) =>
            Interlocked.CompareExchange(ref _saveFailure, exception, null);
    }

    sealed record TenantRegistrationDocument([property: JsonPropertyName("tenant_id")] string TenantId);
    sealed record ProgramRegistrationDocument([property: JsonPropertyName("program_id")] string ProgramId);
    sealed record ProgramDocument(long Revision);
}
