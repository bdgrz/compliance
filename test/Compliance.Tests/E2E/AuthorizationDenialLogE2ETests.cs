using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Bdgrz.Compliance;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class AuthorizationDenialLogE2ETests(BrokerStackFixture broker) : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldLogDeniedActionGivenOutsiderInStandaloneHost()
    {
        // Arrange
        var logs = new CapturingLoggerProvider();
        var bystanderLogs = new CapturingLoggerProvider();
        await using var factory = WithLogs(E2EAppFactory.Create(broker), logs);
        // A second API host in the same process shares the process-wide meter listener.
        await using var bystander = WithLogs(E2EAppFactory.Create(broker), bystanderLogs);
        using var bystanderClient = bystander.CreateClient();

        // Act
        var denial = await DenyOutsiderAsync(factory, logs, "standalone");

        // Assert
        AssertDenialRecord(denial);
        Assert.DoesNotContain(bystanderLogs.Records, record => record.EventName == "AuthorizationDenied");
    }

    [Fact]
    public async Task ShouldLogDeniedActionGivenOutsiderInSplitApiHost()
    {
        // Arrange
        var applicationName = $"compliance-denial-split-{Guid.NewGuid():N}";
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
        using var worker = builder.Build();
        await worker.StartAsync();
        try
        {
            var logs = new CapturingLoggerProvider();
            await using var factory = WithLogs(E2EAppFactory.Create(broker, applicationName), logs);
            var priorMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
            try
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", "api");
                // Act
                var denial = await DenyOutsiderAsync(factory, logs, "split");

                // Assert
                AssertDenialRecord(denial);
            }
            finally
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", priorMode);
            }
        }
        finally
        {
            await worker.StopAsync();
        }
    }

    static WebApplicationFactory<Program> WithLogs(WebApplicationFactory<Program> factory,
        CapturingLoggerProvider logs) =>
        factory.WithWebHostBuilder(host => host.ConfigureLogging(logging => logging.AddProvider(logs)));

    static async Task<DenialAttempt> DenyOutsiderAsync(WebApplicationFactory<Program> factory,
        CapturingLoggerProvider logs, string label)
    {
        using var owner = factory.CreateClient();
        using var outsider = factory.CreateClient();
        await TenantInvitationE2ETests.LoginAsync(owner, $"denial-owner-{label}-{Guid.NewGuid():N}@example.com");
        var outsiderEmail = $"denial-outsider-{label}-{Guid.NewGuid():N}@example.com";
        var outsiderId = await TenantInvitationE2ETests.LoginAsync(outsider, outsiderEmail);
        var slug = $"denial-{Guid.NewGuid():N}"[..24];
        using var registered = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Denial Log",
            slug,
        });
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        var tenant = await registered.Content.ReadFromJsonAsync<TenantRegistrationDocument>();
        Assert.NotNull(tenant);
        var path = $"/api/v1/tenants/{tenant.TenantId}/programs";

        // Wait until the owner's membership is projected so the outsider's denial is not a
        // projection-lag artifact.
        var ownerReady = false;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (!ownerReady && DateTimeOffset.UtcNow < deadline)
        {
            using var ready = await owner.GetAsync(path);
            ownerReady = ready.StatusCode == HttpStatusCode.OK;
            if (!ownerReady)
                await Task.Delay(250);
        }
        Assert.True(ownerReady);

        using var denied = await outsider.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);

        var records = logs.Records
            .Where(record => record.EventName == "AuthorizationDenied")
            .Where(record => Equals(record.Value("actor_id"), outsiderId))
            .ToArray();
        return new DenialAttempt(Assert.Single(records), tenant.TenantId, outsiderEmail, slug);
    }

    static void AssertDenialRecord(DenialAttempt attempt)
    {
        var record = attempt.Record;
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal("Bdgrz.Compliance.Authorization", record.Category);
        Assert.Equal("not_found", record.Value("outcome"));
        Assert.Equal("http", record.Value("transport"));
        Assert.Equal("GET", record.Value("method"));
        Assert.Equal("/api/v1/tenants/{tenant_id}/programs", record.Value("route"));
        Assert.Equal(attempt.TenantId, record.Value("tenant_id"));
        Assert.False(string.IsNullOrEmpty(record.Value("component") as string));
        Assert.False(string.IsNullOrEmpty(record.Value("stage") as string));
        Assert.DoesNotContain(attempt.OutsiderEmail, record.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(attempt.Slug, record.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(record.State,
            pair => pair.Value is string value &&
                (value.Contains(attempt.OutsiderEmail, StringComparison.OrdinalIgnoreCase) ||
                 value.Contains(attempt.Slug, StringComparison.OrdinalIgnoreCase)));
    }

    sealed record DenialAttempt(LogRecord Record, string TenantId, string OutsiderEmail, string Slug);

    sealed record TenantRegistrationDocument(
        [property: System.Text.Json.Serialization.JsonPropertyName("tenant_id")] string TenantId);

    internal sealed record LogRecord(string Category, LogLevel Level, string? EventName, string Message,
        IReadOnlyList<KeyValuePair<string, object?>> State)
    {
        public object? Value(string key) => State.FirstOrDefault(pair => pair.Key == key).Value;
    }

    internal sealed class CapturingLoggerProvider : ILoggerProvider
    {
        readonly ConcurrentQueue<LogRecord> _records = new();

        public IReadOnlyCollection<LogRecord> Records => _records;

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _records);

        public void Dispose()
        {
        }

        sealed class CapturingLogger(string category, ConcurrentQueue<LogRecord> records) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var values = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
                records.Enqueue(new LogRecord(category, logLevel, eventId.Name, formatter(state, exception),
                    values.ToArray()));
            }
        }
    }
}
