using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance;
using Cntryl.Fitz;
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
    public async Task ShouldReturnTransientConflictGivenHeldAppendSessionThroughHttp(
        bool splitHosts)
    {
        // Arrange
        var recorder = new ProgramAggregateWriterRecorder();
        var applicationName = $"compliance-program-concurrency-http-{Guid.NewGuid():N}";
        IHost? worker = null;
        if (splitHosts)
        {
            worker = BuildWorker(applicationName);
            await worker.StartAsync();
        }

        try
        {
            await using var factory = CreateFactory(applicationName, recorder);
            using var client = CreateClient(factory, splitHosts);
            var email = $"program-concurrency-{Guid.NewGuid():N}@example.com";
            await TenantInvitationE2ETests.LoginAsync(client, email);
            var (tenantId, programId, path) = await CreateProgramAsync(client);
            await WaitForRevisionAsync(client, path, 1);
            await using var blockingClient = await CreateFitzClientAsync(broker.WebSocketEndpoint);
            await using var blocker = await blockingClient.Stream.BeginAsync(
                new EventStreamAddress(tenantId, "programs", programId).ToString());

            // Act
            using var conflict = await client.PutAsJsonAsync(path, RevisionRequest("Held-session revision"));
            var conflictBody = await conflict.Content.ReadAsStringAsync();
            using var problem = JsonDocument.Parse(conflictBody);
            var detail = Assert.IsType<string>(problem.RootElement.GetProperty("detail").GetString());

            // Assert
            var contention = Assert.IsType<EventStreamConcurrencyException>(recorder.SaveFailure);
            var fitz = Assert.IsType<Cntryl.Fitz.StreamException>(contention.InnerException);
            Assert.Equal(Cntryl.Fitz.FitzErrorCodes.StreamSessionAlreadyActive, fitz.DomainCode);
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
            Assert.Equal("true", conflict.Headers.GetValues("Portia-Transient").Single());
            Assert.Equal("The request conflicted with a concurrent update.", detail);
            Assert.Equal(path, problem.RootElement.GetProperty("instance").GetString());
            Assert.DoesNotContain("Fitz", detail, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("stream", detail, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(tenantId, detail, StringComparison.Ordinal);
            Assert.DoesNotContain(programId, detail, StringComparison.Ordinal);

            await blocker.RollbackAsync();
            using var accepted = await client.PutAsJsonAsync(path, RevisionRequest("Released-session revision"));
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
            await WaitForRevisionAsync(client, path, 2);
            var revisedEvents = await ReadRevisionsAsync(factory, tenantId, programId);
            var revised = Assert.Single(revisedEvents);
            Assert.Equal(2, revised.Revision);
            Assert.Equal("Released-session revision", revised.Name);
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
    public async Task ShouldReturnTransientConflictGivenHeldAppendSessionThroughMcp(
        bool splitHosts)
    {
        // Arrange
        var recorder = new ProgramAggregateWriterRecorder();
        var applicationName = $"compliance-program-concurrency-mcp-{Guid.NewGuid():N}";
        IHost? worker = null;
        if (splitHosts)
        {
            worker = BuildWorker(applicationName);
            await worker.StartAsync();
        }

        try
        {
            await using var factory = CreateFactory(applicationName, recorder);
            using var client = CreateClient(factory, splitHosts);
            var email = $"program-concurrency-mcp-{Guid.NewGuid():N}@example.com";
            await TenantInvitationE2ETests.LoginAsync(client, email);
            var (tenantId, programId, path) = await CreateProgramAsync(client);
            await WaitForRevisionAsync(client, path, 1);
            await using var mcp = await McpScenario.ConnectAsync(client,
                new Uri(client.BaseAddress!, "/mcp"));
            await using var blockingClient = await CreateFitzClientAsync(broker.WebSocketEndpoint);
            await using var blocker = await blockingClient.Stream.BeginAsync(
                new EventStreamAddress(tenantId, "programs", programId).ToString());

            // Act
            var conflict = await InvokeRevisionAsync(mcp, tenantId, programId,
                "Held-session MCP revision");
            var structured = Assert.IsType<JsonElement>(conflict.StructuredJson);
            var conflictBody = string.Join("\n", conflict.Text);

            // Assert
            var contention = Assert.IsType<EventStreamConcurrencyException>(recorder.SaveFailure);
            var fitz = Assert.IsType<Cntryl.Fitz.StreamException>(contention.InnerException);
            Assert.Equal(Cntryl.Fitz.FitzErrorCodes.StreamSessionAlreadyActive, fitz.DomainCode);
            Assert.True(conflict.IsError);
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

            await blocker.RollbackAsync();
            var accepted = await InvokeRevisionAsync(mcp, tenantId, programId,
                "Released-session MCP revision");
            Assert.False(accepted.IsError);
            await WaitForRevisionAsync(client, path, 2);
            var revisedEvents = await ReadRevisionsAsync(factory, tenantId, programId);
            var revised = Assert.Single(revisedEvents);
            Assert.Equal(2, revised.Revision);
            Assert.Equal("Released-session MCP revision", revised.Name);
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
        ProgramAggregateWriterRecorder recorder) => E2EAppFactory.Create(broker, applicationName)
        .WithWebHostBuilder(host => host.ConfigureTestServices(services =>
        {
            var originalWriter = services.Single(descriptor =>
                descriptor.ServiceType == typeof(IAggregateWriter));
            var originalFactory = originalWriter.ImplementationFactory
                ?? throw new InvalidOperationException("Portia must register an aggregate writer factory.");
            services.Remove(originalWriter);
            services.AddSingleton(recorder);
            services.AddScoped<IAggregateWriter>(provider => new RecordingProgramAggregateWriter(
                (IAggregateWriter)originalFactory(provider),
                provider.GetRequiredService<ProgramAggregateWriterRecorder>()));
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

    sealed class RecordingProgramAggregateWriter(IAggregateWriter inner,
        ProgramAggregateWriterRecorder recorder) : IAggregateWriter
    {
        public async ValueTask SaveAsync<TAggregate>(TAggregate aggregate,
            IExecutionContext context, CancellationToken ct = default)
            where TAggregate : Aggregate
        {
            try
            {
                await inner.SaveAsync(aggregate, context, ct);
            }
            catch (EventStreamConcurrencyException exception) when (aggregate is ComplianceProgram)
            {
                recorder.RecordSaveFailure(exception);
                throw;
            }
        }
    }

    sealed class ProgramAggregateWriterRecorder
    {
        Exception? _saveFailure;

        public Exception? SaveFailure => Volatile.Read(ref _saveFailure);
        public void RecordSaveFailure(EventStreamConcurrencyException exception) =>
            Interlocked.CompareExchange(ref _saveFailure, exception, null);
    }

    sealed record TenantRegistrationDocument([property: JsonPropertyName("tenant_id")] string TenantId);
    sealed record ProgramRegistrationDocument([property: JsonPropertyName("program_id")] string ProgramId);
    sealed record ProgramDocument(long Revision);
}
