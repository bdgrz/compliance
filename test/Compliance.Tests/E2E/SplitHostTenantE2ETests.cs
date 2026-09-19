using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class SplitHostTenantE2ETests(BrokerStackFixture broker)
{
    [Fact]
    public async Task ApiAndIndependentWorkerProjectTenantLifecycle()
    {
        var applicationName = $"compliance-split-e2e-{Guid.NewGuid():N}";
        var previousMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
        using var worker = StartWorker(applicationName);
        try
        {
            await using var factory = E2EAppFactory.Create(broker, applicationName);
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
            using var clientToDispose = client;
            using var login = await client.PostAsJsonAsync("/api/v1/developer-user-sessions",
                new { email_address = $"operator-{Guid.NewGuid():N}@example.com" });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);

            var slug = $"split-{Guid.NewGuid():N}"[..24];
            using var registered = await client.PostAsJsonAsync("/api/v1/tenants", new
            {
                name = "Split Host Tenant",
                legal_name = "Split Host Tenant LLC",
                slug,
            });
            Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
            var registration = await registered.Content.ReadFromJsonAsync<Registration>();
            Assert.NotNull(registration);

            var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
            Tenant? tenant = null;
            while (DateTimeOffset.UtcNow < deadline)
            {
                Assert.False(worker.HasExited, "The independent worker exited before projection completed.");
                using var response = await client.GetAsync($"/api/v1/tenants/{registration.TenantId}");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    tenant = await response.Content.ReadFromJsonAsync<Tenant>();
                    if (tenant?.Status == "active")
                        break;
                }
                await Task.Delay(250);
            }
            Assert.Equal("Split Host Tenant LLC", tenant?.LegalName);
            Assert.Equal("active", tenant?.Status);

            using var otherClient = factory.CreateClient();
            using var otherLogin = await otherClient.PostAsJsonAsync("/api/v1/developer-user-sessions",
                new { email_address = $"other-{Guid.NewGuid():N}@example.com" });
            Assert.Equal(HttpStatusCode.OK, otherLogin.StatusCode);
            using var denied = await otherClient.GetAsync($"/api/v1/tenants/{registration.TenantId}/teams");
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            using var ownTenants = await otherClient.GetAsync("/api/v1/tenants/mine");
            Assert.Equal(HttpStatusCode.OK, ownTenants.StatusCode);
            var ownTenantList = await ownTenants.Content.ReadFromJsonAsync<TenantList>();
            Assert.Empty(ownTenantList?.Items ?? []);

            using var suspended = await client.PostAsync(
                $"/api/v1/tenants/{registration.TenantId}/suspensions", null);
            Assert.Equal(HttpStatusCode.NoContent, suspended.StatusCode);
            Tenant? projected = null;
            while (DateTimeOffset.UtcNow < deadline)
            {
                Assert.False(worker.HasExited, "The independent worker exited before suspension projected.");
                using var response = await client.GetAsync($"/api/v1/tenants/{registration.TenantId}");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    projected = await response.Content.ReadFromJsonAsync<Tenant>();
                    if (projected?.Status == "suspended")
                        break;
                }
                await Task.Delay(250);
            }
            Assert.Equal("suspended", projected?.Status);
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", previousMode);
            if (!worker.HasExited)
            {
                worker.Kill(entireProcessTree: true);
                await worker.WaitForExitAsync();
            }
        }
    }

    Process StartWorker(string applicationName)
    {
        var start = new ProcessStartInfo("dotnet");
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        start.Environment["COMPLIANCE_HOST_MODE"] = "worker";
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        start.Environment["BDGRZ_DEVELOPER_AUTH"] = "true";
        start.Environment["Fitz__Endpoint"] = broker.WebSocketEndpoint;
        start.Environment["Fitz__ApplicationName"] = applicationName;
        start.Environment["Fitz__StartupTimeoutSeconds"] = "30";
        return Process.Start(start) ?? throw new InvalidOperationException("Could not start the worker host.");
    }

    sealed record Registration([property: JsonPropertyName("tenant_id")] string TenantId);
    sealed record Tenant([property: JsonPropertyName("legal_name")] string? LegalName, string Status);
    sealed record TenantList(IReadOnlyList<Tenant> Items);
}
