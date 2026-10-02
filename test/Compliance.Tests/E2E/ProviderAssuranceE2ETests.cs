using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ProviderAssuranceE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRetainReportsAndPersonalReviewsGivenStandaloneOrSplitHostAndWorkerRestart(bool split)
    {
        // Arrange
        var applicationName = $"compliance-assurance-{Guid.NewGuid():N}";
        using var worker = split ? BuildWorker(applicationName) : null;
        if (worker is not null)
            await worker.StartAsync();
        await using var factory = E2EAppFactory.Create(broker, applicationName);
        var previousMode = TestHostMode.Current;
        HttpClient client;
        try
        {
            TestHostMode.Set(split ? "api" : "standalone");
            client = factory.CreateClient();
        }
        finally
        {
            TestHostMode.Set(previousMode);
        }
        using var owner = client;
        await TenantInvitationE2ETests.LoginAsync(owner, $"assurance-{Guid.NewGuid():N}@example.com");
        var tenantId = await CreateTenantAsync(owner);
        await AccessGrantE2ESupport.IssueFounderOrganizationGrantAsync(owner, tenantId);
        var providerId = await RecordProviderAsync(owner, tenantId);
        var path = $"/api/v1/tenants/{tenantId}/providers/{providerId}";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodEnd = today.AddDays(-60);

        // Act
        using var recorded = await owner.PostAsJsonAsync($"{path}/assurance-reports", new { content = Report(periodEnd, "Example Assurance LLP") });
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        var reportId = (await recorded.Content.ReadFromJsonAsync<ReportRegistration>())!.ReportId;
        using var review = await owner.PostAsJsonAsync($"{path}/reviews", new
        {
            content = new
            {
                reviewed_at = Iso(today.AddDays(-30)),
                next_review_due = Iso(today.AddDays(300)),
                evidence_kind = "soc2_type2",
                conclusion = "acceptable",
                rationale = "Reviewed against our controls.",
                assurance_report_id = reportId,
            },
        });
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        await WaitForListAsync(owner, $"{path}/assurance-reports", 1);
        await WaitForListAsync(owner, $"{path}/reviews", 1);
        if (worker is not null)
            await worker.StopAsync();
        using var revised = await owner.PutAsJsonAsync($"{path}/assurance-reports/{reportId}", new
        {
            expected_revision = 1,
            content = Report(periodEnd, "Renamed Assurance LLP"),
        });
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
        if (split)
        {
            using var lagging = await owner.GetAsync($"{path}/assurance-reports");
            Assert.Equal(HttpStatusCode.Conflict, lagging.StatusCode);
            Assert.Equal("true", lagging.Headers.GetValues("Portia-Transient").Single());
        }
        using var restartedWorker = split ? BuildWorker(applicationName) : null;
        if (restartedWorker is not null)
            await restartedWorker.StartAsync();
        var issuer = await WaitForIssuerAsync(owner, $"{path}/assurance-reports", "Renamed Assurance LLP");

        // Assert
        Assert.Equal("Renamed Assurance LLP", issuer);
        using var coverageResponse = await owner.GetAsync($"{path}/assurance-coverage");
        Assert.Equal(HttpStatusCode.OK, coverageResponse.StatusCode);
        var coverage = await coverageResponse.Content.ReadFromJsonAsync<CoverageDocument>();
        Assert.Equal("current", coverage!.Status);
        Assert.False(coverage.Complete);
        Assert.Contains("uncovered_after_period", coverage.Reasons);
        await using var mcp = await McpScenario.ConnectAsync(owner, new Uri(owner.BaseAddress!, "/mcp"));
        var mcpReviews = await mcp.When("bdgrz.provider.reviews.list", new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId,
            ["provider_id"] = providerId,
        }).ExpectSuccess();
        Assert.Equal(1, Assert.IsType<JsonElement>(mcpReviews.StructuredJson).GetProperty("result").GetProperty("items").GetArrayLength());
        var otherTenantId = await CreateTenantAsync(owner);
        await AccessGrantE2ESupport.IssueFounderOrganizationGrantAsync(owner, otherTenantId);
        var otherProviderId = await RecordProviderAsync(owner, otherTenantId);
        using var foreignReports = await owner.GetAsync($"/api/v1/tenants/{otherTenantId}/providers/{providerId}/assurance-reports");
        using var foreignCoverage = await owner.GetAsync($"/api/v1/tenants/{otherTenantId}/providers/{providerId}/assurance-coverage");
        using var otherCoverage = await owner.GetAsync($"/api/v1/tenants/{otherTenantId}/providers/{otherProviderId}/assurance-coverage");
        Assert.Equal(HttpStatusCode.NotFound, foreignReports.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignCoverage.StatusCode);
        Assert.Equal("no_review", (await otherCoverage.Content.ReadFromJsonAsync<CoverageDocument>())!.Status);
        if (restartedWorker is not null)
            await restartedWorker.StopAsync();
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
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
        return builder.Build();
    }

    static object Report(DateOnly periodEnd, string issuer) => new
    {
        report_kind = "soc2_type2",
        issuer,
        scope = "Hosting platform",
        period_start = Iso(periodEnd.AddDays(-364)),
        period_end = Iso(periodEnd),
        opinion = "unqualified",
        opinion_source = "Independent service auditor's report, section I",
        citation = new
        {
            artifact_kind = "soc2_report",
            title = "Annual SOC 2 Type 2",
            version_or_date = Iso(periodEnd),
            locator = "report.pdf page 4",
            metadata_classification = "restricted",
        },
    };

    static readonly string[] CustomerData = ["customer_data"];

    static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    static async Task<Guid> CreateTenantAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Assurance tenant",
            slug = $"assurance-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TenantDocument>())!.TenantId;
    }

    static async Task<Guid> RecordProviderAsync(HttpClient client, Guid tenantId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/providers", new
            {
                content = new
                {
                    name = "Hosting supplier",
                    provider_kind = "vendor",
                    materiality = "material",
                    materiality_basis = CustomerData,
                    materiality_rationale = "Processes customer data",
                },
            });
            if (response.StatusCode == HttpStatusCode.OK)
                return (await response.Content.ReadFromJsonAsync<ProviderRegistration>())!.ProviderId;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("The explicit organization provider management grant was not projected.");
    }

    static async Task WaitForListAsync(HttpClient client, string path, int count)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (json.RootElement.GetProperty("items").GetArrayLength() == count)
                    return;
            }
            else
            {
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            }
            await Task.Delay(250);
        }
        throw new TimeoutException($"{path} was not projected.");
    }

    static async Task<string> WaitForIssuerAsync(HttpClient client, string path, string issuer)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var current = json.RootElement.GetProperty("items")[0].GetProperty("content").GetProperty("issuer").GetString()!;
                if (current == issuer)
                    return current;
            }
            else
            {
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            }
            await Task.Delay(250);
        }
        throw new TimeoutException($"{path} did not reach the revised report.");
    }

    sealed record TenantDocument([property: JsonPropertyName("tenant_id")] Guid TenantId);
    sealed record ProviderRegistration([property: JsonPropertyName("provider_id")] Guid ProviderId);
    sealed record ReportRegistration([property: JsonPropertyName("report_id")] Guid ReportId);
    sealed record CoverageDocument(string Status, bool Complete, string[] Reasons);
}
