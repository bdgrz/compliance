using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Bdgrz.Compliance;
using Cntryl.Portia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class SplitHostEmailDeliveryE2ETests(BrokerStackFixture broker) : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldDeliverSameChallengeGivenWorkerRestartAfterUnacknowledgedSend()
    {
        // Arrange
        var applicationName = $"compliance-email-retry-{Guid.NewGuid():N}";
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var firstDelivery = new RecordingDelivery(failAfterSend: true);
        var workerLogs = new AuthorizationDenialLogE2ETests.CapturingLoggerProvider();
        var apiLogs = new AuthorizationDenialLogE2ETests.CapturingLoggerProvider();
        using var firstWorker = CreateWorker(applicationName, key, firstDelivery, workerLogs);
        await firstWorker.StartAsync();
        await using var factory = E2EAppFactory.Create(broker, applicationName)
            .WithWebHostBuilder(host =>
            {
                host.UseSetting("Compliance:EmailDelivery:ActiveTokenKeyId", "test-key");
                host.UseSetting("Compliance:EmailDelivery:TokenKeys:test-key", key);
                host.ConfigureLogging(logging => logging.AddProvider(apiLogs));
            });
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
        var email = $"restart-{Guid.NewGuid():N}@example.com";
        var userId = await TenantInvitationE2ETests.LoginAsync(owner, email);
        var path = $"/api/v1/users/{userId}/email-addresses/{email}";
        await WaitForStatusAsync(owner, path, "not_issued");

        // Act: the first worker hands the message to the provider, then loses its acknowledgment.
        using var issued = await owner.PostAsync($"{path}/challenges", null);
        Assert.Equal(HttpStatusCode.NoContent, issued.StatusCode);
        await WaitForStatusAsync(owner, path, "failed");
        await firstWorker.StopAsync();
        var retryDelivery = new RecordingDelivery(failAfterSend: false);
        using var secondWorker = CreateWorker(applicationName, key, retryDelivery, workerLogs);
        await secondWorker.StartAsync();
        try
        {
            await WaitForStatusAsync(owner, path, "delivered");

            // Assert
            var firstAttempt = Assert.Single(firstDelivery.Attempts.Distinct());
            var secondAttempt = Assert.Single(retryDelivery.Attempts.Distinct());
            Assert.Equal(firstAttempt, secondAttempt);
            using var denied = await owner.GetAsync(
                $"/api/v1/users/{Guid.NewGuid()}/email-addresses/{email}/challenges/status");
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            using var status = await owner.GetAsync($"{path}/challenges/status");
            var body = await status.Content.ReadAsStringAsync();
            Assert.DoesNotContain(email, body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(firstAttempt.Token, body, StringComparison.Ordinal);
            using var completed = await owner.PostAsJsonAsync($"{path}/verifications",
                new { token = firstAttempt.Token });
            Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);
            var logText = string.Join('\n', workerLogs.Records.Concat(apiLogs.Records)
                .SelectMany(record => new[] { record.Message }.Concat(
                    record.State.Select(item => $"{item.Key}={item.Value}"))));
            Assert.DoesNotContain(email, logText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(firstAttempt.Token, logText, StringComparison.Ordinal);
            Assert.DoesNotContain(key, logText, StringComparison.Ordinal);
            Assert.DoesNotContain("provider-secret", logText, StringComparison.Ordinal);
        }
        finally
        {
            await secondWorker.StopAsync();
        }
    }

    IHost CreateWorker(string applicationName, string key, RecordingDelivery delivery,
        ILoggerProvider logs)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
        builder.Configuration["Compliance:EmailDelivery:ActiveTokenKeyId"] = "test-key";
        builder.Configuration["Compliance:EmailDelivery:TokenKeys:test-key"] = key;
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
        builder.Services.AddSingleton<IEmailChallengeDelivery>(delivery);
        builder.Logging.AddProvider(logs);
        return builder.Build();
    }

    static async Task WaitForStatusAsync(HttpClient client, string path, string expected)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync($"{path}/challenges/status");
            if (response.StatusCode == HttpStatusCode.OK &&
                (await response.Content.ReadFromJsonAsync<StatusDocument>())?.DeliveryStatus == expected)
                return;
            await Task.Delay(250);
        }
        Assert.Fail($"Email challenge status did not become {expected}.");
    }

    sealed class RecordingDelivery(bool failAfterSend) : IEmailChallengeDelivery
    {
        public ConcurrentQueue<(Uuid ChallengeId, string Token)> Attempts { get; } = new();

        public ValueTask SendAsync(Uuid challengeId, Uuid userId, string emailAddress,
            string token, CancellationToken ct)
        {
            _ = userId;
            _ = emailAddress;
            ct.ThrowIfCancellationRequested();
            Attempts.Enqueue((challengeId, token));
            if (failAfterSend)
                throw new InvalidOperationException("provider-secret");
            return ValueTask.CompletedTask;
        }
    }

    sealed record StatusDocument([property: JsonPropertyName("delivery_status")] string DeliveryStatus);
}
