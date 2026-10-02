using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using static Bdgrz.Compliance.Tests.Features.Providers.AssuranceSamples;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderAssuranceHttpMcpTests
{
    [Fact]
    public async Task ShouldRecordReviewAndReadCoverageGivenAuthorizedHttpCaller()
    {
        // Arrange
        await using var factory = ProviderHttpMcpTests.CreateFactory();
        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/developer-user-sessions", new { email_address = "assurance-http@example.com" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tenantId = Uuid.CreateVersion4();
        var otherTenant = Uuid.CreateVersion4();
        var providerId = await RecordProviderAsync(client, tenantId);
        var otherProviderId = await RecordProviderAsync(client, otherTenant);

        // Act
        using var recorded = await client.PostAsync($"{Path(tenantId, providerId)}/assurance-reports", Json("content", Report()));
        var report = JsonSerializer.Deserialize(await recorded.Content.ReadAsStringAsync(), ComplianceCoreJsonContext.Default.AssuranceReportRegistration)!;
        using var revised = await client.PutAsync($"{Path(tenantId, providerId)}/assurance-reports/{report.ReportId}",
            new StringContent("{\"expected_revision\":1,\"content\":" + JsonSerializer.Serialize(Report() with { Issuer = "Renamed LLP" },
                ComplianceCoreJsonContext.Default.AssuranceReportContent) + "}", Encoding.UTF8, "application/json"));
        using var review = await client.PostAsync($"{Path(tenantId, providerId)}/reviews", Json("content", Review(report.ReportId)));
        using var invalid = await client.PostAsync($"{Path(tenantId, providerId)}/reviews",
            Json("content", Review(report.ReportId) with { Conclusion = "approved" }));
        await CatchUpAsync(factory.Services, tenantId);
        using var reports = await client.GetAsync($"{Path(tenantId, providerId)}/assurance-reports?limit=10");
        using var reviews = await client.GetAsync($"{Path(tenantId, providerId)}/reviews");
        using var coverage = await client.GetAsync($"{Path(tenantId, providerId)}/assurance-coverage?as_of=2026-12-31");
        using var foreignProvider = await client.GetAsync($"{Path(otherTenant, providerId)}/assurance-reports");
        using var foreignCoverage = await client.GetAsync($"{Path(tenantId, otherProviderId)}/assurance-coverage");
        using var foreignRecord = await client.PostAsync($"{Path(otherTenant, providerId)}/assurance-reports", Json("content", Report()));
        using var otherReports = await client.GetAsync($"{Path(otherTenant, otherProviderId)}/assurance-reports");

        // Assert
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reports.StatusCode);
        using var reportPage = JsonDocument.Parse(await reports.Content.ReadAsStringAsync());
        var item = Assert.Single(reportPage.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(2, item.GetProperty("revision").GetInt64());
        Assert.Equal("soc2_type2", item.GetProperty("content").GetProperty("report_kind").GetString());
        Assert.Equal("Renamed LLP", item.GetProperty("content").GetProperty("issuer").GetString());
        Assert.Equal("2026-06-30", item.GetProperty("content").GetProperty("period_end").GetString());
        Assert.Equal(0, item.GetProperty("exception_count").GetInt32());
        using var reviewPage = JsonDocument.Parse(await reviews.Content.ReadAsStringAsync());
        Assert.Equal("acceptable", Assert.Single(reviewPage.RootElement.GetProperty("items").EnumerateArray()).GetProperty("content").GetProperty("conclusion").GetString());
        using var view = JsonDocument.Parse(await coverage.Content.ReadAsStringAsync());
        Assert.Equal("current", view.RootElement.GetProperty("status").GetString());
        Assert.Equal("2026-12-31", view.RootElement.GetProperty("as_of").GetString());
        Assert.Equal("uncovered_after_period", view.RootElement.GetProperty("reasons")[0].GetString());
        Assert.Equal(HttpStatusCode.NotFound, foreignProvider.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignCoverage.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignRecord.StatusCode);
        using var otherPage = JsonDocument.Parse(await otherReports.Content.ReadAsStringAsync());
        Assert.Equal(0, otherPage.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task ShouldRecordReportAndReadCoverageGivenAuthorizedMcpCaller()
    {
        // Arrange
        await using var factory = ProviderHttpMcpTests.CreateFactory();
        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/developer-user-sessions", new { email_address = "assurance-mcp@example.com" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tenantId = Uuid.CreateVersion4();
        var providerId = await RecordProviderAsync(client, tenantId);
        await using var scenario = await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp"));

        // Act
        var recorded = await scenario.When("bdgrz.provider.assurance_report.record", new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId.ToString(),
            ["provider_id"] = providerId.ToString(),
            ["content"] = JsonSerializer.SerializeToElement(Report(), ComplianceCoreJsonContext.Default.AssuranceReportContent),
        }).ExpectSuccess();
        var report = Result(recorded).Deserialize(ComplianceCoreJsonContext.Default.AssuranceReportRegistration)!;
        var revised = await scenario.When("bdgrz.provider.assurance_report.revise", new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId.ToString(),
            ["provider_id"] = providerId.ToString(),
            ["report_id"] = report.ReportId.ToString(),
            ["expected_revision"] = 1,
            ["content"] = JsonSerializer.SerializeToElement(Report() with { Issuer = "Renamed LLP" }, ComplianceCoreJsonContext.Default.AssuranceReportContent),
        }).ExpectSuccess();
        await CatchUpAsync(factory.Services, tenantId);
        var reports = await scenario.When("bdgrz.provider.assurance_reports.list", new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId.ToString(),
            ["provider_id"] = providerId.ToString(),
        }).ExpectSuccess();
        var reviews = await scenario.When("bdgrz.provider.reviews.list", new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId.ToString(),
            ["provider_id"] = providerId.ToString(),
        }).ExpectSuccess();
        var coverage = await scenario.When("bdgrz.provider.assurance_coverage.get", new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId.ToString(),
            ["provider_id"] = providerId.ToString(),
            ["as_of"] = "2026-06-30",
        }).ExpectSuccess();

        // Assert
        Assert.Equal(2, Result(revised).GetProperty("revision").GetInt64());
        var item = Assert.Single(Result(reports).GetProperty("items").EnumerateArray());
        Assert.Equal("Renamed LLP", item.GetProperty("content").GetProperty("issuer").GetString());
        Assert.Equal(0, Result(reviews).GetProperty("items").GetArrayLength());
        Assert.Equal("no_review", Result(coverage).GetProperty("status").GetString());
        Assert.Equal("covers_as_of", Assert.Single(Result(coverage).GetProperty("reports").EnumerateArray()).GetProperty("period_coverage").GetString());
    }

    static JsonElement Result(McpCallSnapshot snapshot) => Assert.IsType<JsonElement>(snapshot.StructuredJson).GetProperty("result");

    static string Path(Uuid tenant, Uuid provider) => $"/api/v1/tenants/{tenant}/providers/{provider}";

    static StringContent Json(string name, AssuranceReportContent content) => new("{\"" + name + "\":" +
        JsonSerializer.Serialize(content, ComplianceCoreJsonContext.Default.AssuranceReportContent) + "}", Encoding.UTF8, "application/json");

    static StringContent Json(string name, ProviderReviewContent content) => new("{\"" + name + "\":" +
        JsonSerializer.Serialize(content, ComplianceCoreJsonContext.Default.ProviderReviewContent) + "}", Encoding.UTF8, "application/json");

    static async Task<Uuid> RecordProviderAsync(HttpClient client, Uuid tenantId)
    {
        using var body = new StringContent("{\"content\":" + JsonSerializer.Serialize(MaterialProvider(),
            ComplianceCoreJsonContext.Default.ProviderContent) + "}", Encoding.UTF8, "application/json");
        using var created = await client.PostAsync($"/api/v1/tenants/{tenantId}/providers", body);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        return JsonSerializer.Deserialize(await created.Content.ReadAsStringAsync(), ComplianceCoreJsonContext.Default.ProviderRegistration)!.ProviderId;
    }

    static async Task CatchUpAsync(IServiceProvider provider, Uuid tenant)
    {
        using var scope = provider.CreateScope();
        var directory = scope.ServiceProvider.GetRequiredService<FitzAssuranceDirectory>();
        var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var checkpoint = await directory.LoadCheckpointAsync(tenant);
        var pattern = EventStreamPattern.ForPattern(tenant.ToString(), ProviderAssuranceRegister.Area);
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(FitzAssuranceDirectory.ProjectorName, pattern), checkpoint));
        var cursor = checkpoint.Cursor;
        await foreach (var record in events.ReadAsync(pattern, checkpoint.Cursor, CancellationToken.None))
        {
            await directory.ApplyAsync(record.Event);
            cursor = record.NextCursor;
        }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }
}
