using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class OperatorPortfolioE2ETests(BrokerStackFixture broker) : IClassFixture<BrokerStackFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldListPlatformTenantMetadataGivenConfiguredOperatorAndHostMode(bool splitHosts)
    {
        // Arrange
        var applicationName = $"compliance-operator-portfolio-{Guid.NewGuid():N}";
        var operatorEmail = $"operator-{Guid.NewGuid():N}@example.com";
        var ordinaryEmail = $"ordinary-{Guid.NewGuid():N}@example.com";
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
            builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
            worker = builder.Build();
            await worker.StartAsync();
        }

        try
        {
            Uuid operatorId;
            Uuid suspendedTenantId;
            Uuid activeTenantId;
            await using (var seedFactory = E2EAppFactory.Create(broker, applicationName))
            {
                using var seedClient = CreateClient(seedFactory, splitHosts);
                operatorId = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(seedClient,
                    operatorEmail), CultureInfo.InvariantCulture);
                suspendedTenantId = await RegisterAsync(seedClient, "Suspended");
                using var ordinarySeedClient = CreateClient(seedFactory, splitHosts);
                await TenantInvitationE2ETests.LoginAsync(ordinarySeedClient, ordinaryEmail);
                activeTenantId = await RegisterAsync(ordinarySeedClient, "Active");
                await WaitForActiveAsync(seedClient, suspendedTenantId);
                using var suspended = await seedClient.PostAsync(
                    $"/api/v1/tenants/{suspendedTenantId}/suspensions", null);
                Assert.Equal(HttpStatusCode.NoContent, suspended.StatusCode);
            }

            await using var factory = E2EAppFactory.Create(broker, applicationName)
                .WithWebHostBuilder(host => host.ConfigureTestServices(services =>
                    services.AddSingleton(new PlatformOperatorAuthority([operatorId]))));
            using var operatorClient = CreateClient(factory, splitHosts);
            using var ordinaryClient = CreateClient(factory, splitHosts);
            Assert.Equal(operatorId, Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(
                operatorClient, operatorEmail), CultureInfo.InvariantCulture));
            await TenantInvitationE2ETests.LoginAsync(ordinaryClient, ordinaryEmail);
            const string path = "/api/v1/platform/tenants";

            // Act
            var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
            var observed = new Dictionary<Uuid, string>();
            while (DateTimeOffset.UtcNow < deadline)
            {
                observed = await ReadAllAsync(operatorClient, path);
                if (observed.GetValueOrDefault(suspendedTenantId) == "suspended" &&
                    observed.GetValueOrDefault(activeTenantId) == "active")
                    break;
                await Task.Delay(250);
            }

            // Assert
            Assert.True(observed.GetValueOrDefault(suspendedTenantId) == "suspended",
                $"Expected suspended tenant {suspendedTenantId}; observed {string.Join(", ", observed)}");
            Assert.True(observed.GetValueOrDefault(activeTenantId) == "active",
                $"Expected active tenant {activeTenantId}; observed {string.Join(", ", observed)}");
            using var firstPage = await operatorClient.GetAsync($"{path}?limit=1");
            using var firstDocument = JsonDocument.Parse(await firstPage.Content.ReadAsStreamAsync());
            var cursor = firstDocument.RootElement.GetProperty("next_cursor").GetString();
            Assert.NotNull(cursor);
            Assert.Single(firstDocument.RootElement.GetProperty("items").EnumerateArray());
            using var secondPage = await operatorClient.GetAsync(
                $"{path}?limit=1&cursor={Uri.EscapeDataString(cursor)}");
            using var secondDocument = JsonDocument.Parse(await secondPage.Content.ReadAsStreamAsync());
            Assert.Single(secondDocument.RootElement.GetProperty("items").EnumerateArray());
            Assert.NotEqual(
                firstDocument.RootElement.GetProperty("items")[0].GetProperty("tenant_id").GetString(),
                secondDocument.RootElement.GetProperty("items")[0].GetProperty("tenant_id").GetString());

            foreach (var query in new[] { "?limit=0", "?limit=201", "?cursor=not-a-cursor" })
            {
                using var invalid = await operatorClient.GetAsync($"{path}{query}");
                Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            }

            using var denied = await ordinaryClient.GetAsync(path);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            using var unauthenticated = factory.CreateClient();
            using var unauthorized = await unauthenticated.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
            using var noClientRecordAccess = await operatorClient.GetAsync(
                $"/api/v1/tenants/{activeTenantId}/programs");
            Assert.Equal(HttpStatusCode.NotFound, noClientRecordAccess.StatusCode);

            await using var operatorMcp = await McpScenario.ConnectAsync(operatorClient,
                new Uri(operatorClient.BaseAddress!, "/mcp"));
            _ = await operatorMcp.When("bdgrz.platform.tenant.list",
                new Dictionary<string, object?> { ["limit"] = 2 }).ExpectSuccess();
            foreach (var invalidInput in new[]
                     {
                         new Dictionary<string, object?> { ["limit"] = 0 },
                         new Dictionary<string, object?> { ["limit"] = 201 },
                         new Dictionary<string, object?> { ["cursor"] = "not-a-cursor" },
                     })
                _ = await operatorMcp.When("bdgrz.platform.tenant.list", invalidInput)
                    .ExpectFailure("Validation");
            await using var ordinaryMcp = await McpScenario.ConnectAsync(ordinaryClient,
                new Uri(ordinaryClient.BaseAddress!, "/mcp"));
            _ = await ordinaryMcp.When("bdgrz.platform.tenant.list",
                new Dictionary<string, object?>()).ExpectFailure();
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

    static HttpClient CreateClient(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,
        bool splitHosts)
    {
        var priorMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
        try
        {
            if (splitHosts)
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", "api");
            return factory.CreateClient();
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", priorMode);
        }
    }

    static async Task<Uuid> RegisterAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/tenants", new
        {
            name,
            slug = $"portfolio-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        return Uuid.Parse(document.RootElement.GetProperty("tenant_id").GetString()!,
            CultureInfo.InvariantCulture);
    }

    static async Task WaitForActiveAsync(HttpClient client, Uuid tenantId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync($"/api/v1/tenants/{tenantId}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
                if (document.RootElement.GetProperty("status").GetString() == "active")
                    return;
            }
            await Task.Delay(250);
        }
        Assert.Fail("The tenant did not become active before suspension.");
    }

    static async Task<Dictionary<Uuid, string>> ReadAllAsync(HttpClient client, string path)
    {
        var observed = new Dictionary<Uuid, string>();
        string? cursor = null;
        do
        {
            var query = $"{path}?limit=2";
            if (cursor is not null)
                query += $"&cursor={Uri.EscapeDataString(cursor)}";
            using var response = await client.GetAsync(query);
            Assert.True(response.StatusCode == HttpStatusCode.OK,
                $"Portfolio returned {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
            foreach (var item in document.RootElement.GetProperty("items").EnumerateArray())
                observed[Uuid.Parse(item.GetProperty("tenant_id").GetString()!, CultureInfo.InvariantCulture)] =
                    item.GetProperty("status").GetString()!;
            cursor = document.RootElement.GetProperty("next_cursor").GetString();
        } while (cursor is not null);
        return observed;
    }
}
