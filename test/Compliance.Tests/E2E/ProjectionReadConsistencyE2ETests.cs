using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     M0-A05 spike: a projection-derived read never returns an older successful view, recovers
///     after projection catch-up, and never discloses one client organization's projection through
///     another organization's route, in both standalone and split API/worker hosts.
/// </summary>
[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ProjectionReadConsistencyE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldNeverReturnStaleProjectionOrCrossTenantRowsGivenStandaloneOrSplitHost(
        bool splitHosts)
    {
        // Arrange
        var applicationName = $"compliance-projection-read-{Guid.NewGuid():N}";
        IHost? worker = null;
        IHost? restartedWorker = null;
        var workerStopped = false;
        if (splitHosts)
        {
            worker = BuildWorker(applicationName);
            await worker.StartAsync();
        }

        try
        {
            await using var factory = E2EAppFactory.Create(broker, applicationName);
            var previousMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
            HttpClient owner;
            try
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE",
                    splitHosts ? "api" : "standalone");
                owner = factory.CreateClient();
            }
            finally
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", previousMode);
            }

            using (owner)
            using (var outsider = factory.CreateClient())
            {
                await TenantInvitationE2ETests.LoginAsync(owner,
                    $"projection-owner-{Guid.NewGuid():N}@example.com");
                await TenantInvitationE2ETests.LoginAsync(outsider,
                    $"projection-outsider-{Guid.NewGuid():N}@example.com");
                var tenantA = await CreateTenantAsync(owner, "Projection client A");
                var tenantB = await CreateTenantAsync(owner, "Projection client B");
                var applicationsA = $"/api/v1/tenants/{tenantA}/applications";
                var applicationsB = $"/api/v1/tenants/{tenantB}/applications";
                var applicationId = await PostUntilOkAsync(owner, applicationsA,
                    new { name = "Payroll", purpose = "Run payroll" }, "application_id");
                var programId = await PostUntilOkAsync(owner,
                    $"/api/v1/tenants/{tenantA}/programs", new
                    {
                        name = "Projection program",
                        plan = new
                        {
                            target_readiness_date = "2027-01-31",
                            target_type_i_as_of_date = "2027-03-31",
                            target_type_ii_start_date = "2027-04-01",
                            target_type_ii_end_date = "2028-03-31",
                            readiness_advisor = "Advisor",
                            audit_firm = (string?)null,
                        },
                    }, "program_id");
                var referencesPath =
                    $"{applicationsA}/{applicationId}/boundary-references";
                _ = await WaitForOkAsync(owner, referencesPath);
                _ = await WaitForOkAsync(owner, applicationsB);
                if (worker is not null)
                {
                    await worker.StopAsync();
                    workerStopped = true;
                }

                // Act
                using var boundaryResponse = await owner.PostAsJsonAsync(
                    $"/api/v1/tenants/{tenantA}/programs/{programId}/boundaries",
                    new { content = BoundaryContent(applicationId) });
                Assert.Equal(HttpStatusCode.OK, boundaryResponse.StatusCode);
                var boundaryId = (await ReadAsync(boundaryResponse))
                    .GetProperty("boundary_id").GetString()!;
                if (splitHosts)
                {
                    using var pending = await owner.GetAsync(referencesPath);
                    AssertTransientConflict(pending);
                    restartedWorker = BuildWorker(applicationName);
                    await restartedWorker.StartAsync();
                }

                JsonElement? caughtUp = null;
                var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
                while (caughtUp is null && DateTimeOffset.UtcNow < deadline)
                {
                    using var response = await owner.GetAsync(referencesPath);
                    if (response.StatusCode == HttpStatusCode.Conflict)
                    {
                        AssertTransientConflict(response);
                        await Task.Delay(100);
                        continue;
                    }
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    var page = await ReadAsync(response);
                    Assert.Contains(page.GetProperty("items").EnumerateArray(),
                        item => item.GetProperty("boundary_id").GetString() == boundaryId);
                    caughtUp = page;
                }

                // Assert
                Assert.True(caughtUp is not null,
                    "The boundary-reference projection did not catch up before the deadline.");
                using (var crossTenantReferences = await owner.GetAsync(
                           $"{applicationsB}/{applicationId}/boundary-references"))
                using (var crossTenantApplication = await owner.GetAsync(
                           $"{applicationsB}/{applicationId}"))
                using (var absentInTenantB = await owner.GetAsync(
                           $"{applicationsB}/{Guid.NewGuid()}/boundary-references"))
                using (var tenantBApplications = await owner.GetAsync(applicationsB))
                using (var outsiderReferences = await outsider.GetAsync(referencesPath))
                using (var unknownTenantReferences = await outsider.GetAsync(
                           $"/api/v1/tenants/{Guid.NewGuid()}/applications/{applicationId}" +
                           "/boundary-references"))
                {
                    Assert.Equal(HttpStatusCode.NotFound, crossTenantReferences.StatusCode);
                    Assert.Equal(HttpStatusCode.NotFound, crossTenantApplication.StatusCode);
                    Assert.Equal(absentInTenantB.StatusCode, crossTenantReferences.StatusCode);
                    var absentProblem = await ReadAsync(absentInTenantB);
                    var crossTenantProblem = await ReadAsync(crossTenantReferences);
                    Assert.Equal(absentProblem.GetProperty("title").GetString(),
                        crossTenantProblem.GetProperty("title").GetString());
                    Assert.Equal(absentProblem.GetProperty("detail").GetString(),
                        crossTenantProblem.GetProperty("detail").GetString());
                    Assert.Equal(HttpStatusCode.OK, tenantBApplications.StatusCode);
                    Assert.Empty((await ReadAsync(tenantBApplications))
                        .GetProperty("items").EnumerateArray());
                    Assert.Equal(HttpStatusCode.NotFound, outsiderReferences.StatusCode);
                    Assert.Equal(unknownTenantReferences.StatusCode, outsiderReferences.StatusCode);
                    var unknownProblem = await ReadAsync(unknownTenantReferences);
                    var outsiderProblem = await ReadAsync(outsiderReferences);
                    Assert.Equal(unknownProblem.GetProperty("title").GetString(),
                        outsiderProblem.GetProperty("title").GetString());
                    Assert.Equal(unknownProblem.GetProperty("detail").GetString(),
                        outsiderProblem.GetProperty("detail").GetString());
                }
                await using (var mcp = await McpScenario.ConnectAsync(owner,
                                 new Uri(owner.BaseAddress!, "/mcp")))
                {
                    _ = await mcp.When("bdgrz.application.boundary_references.list",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = tenantB,
                            ["application_id"] = applicationId,
                        }).ExpectFailure("NotFound");
                    _ = await mcp.When("bdgrz.application.boundary_references.list",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = tenantA,
                            ["application_id"] = applicationId,
                        }).ExpectSuccess();
                }
                await using (var mcp = await McpScenario.ConnectAsync(outsider,
                                 new Uri(outsider.BaseAddress!, "/mcp")))
                {
                    _ = await mcp.When("bdgrz.application.boundary_references.list",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = tenantA,
                            ["application_id"] = applicationId,
                        }).ExpectFailure("NotFound");
                }
            }
        }
        finally
        {
            if (restartedWorker is not null)
            {
                await restartedWorker.StopAsync();
                restartedWorker.Dispose();
            }
            if (worker is not null)
            {
                if (!workerStopped)
                    await worker.StopAsync();
                worker.Dispose();
            }
        }
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

    static void AssertTransientConflict(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("true", response.Headers.GetValues("Portia-Transient").Single());
    }

    static async Task<string> CreateTenantAsync(HttpClient owner, string name)
    {
        using var response = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name,
            slug = $"projection-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("tenant_id").GetString()!;
    }

    static async Task<string> PostUntilOkAsync(HttpClient client, string path, object body,
        string idProperty)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.PostAsJsonAsync(path, body);
            if (response.StatusCode == HttpStatusCode.OK)
                return (await ReadAsync(response)).GetProperty(idProperty).GetString()!;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException($"POST {path} remained unauthorized after bootstrap.");
    }

    static async Task<JsonElement> WaitForOkAsync(HttpClient client, string path)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
                return await ReadAsync(response);
            Assert.True(response.StatusCode is HttpStatusCode.NotFound
                    or HttpStatusCode.Forbidden or HttpStatusCode.Conflict,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException($"GET {path} did not succeed before the deadline.");
    }

    static object BoundaryContent(string applicationId) => new
    {
        statement = "Projection consistency boundary.",
        engagement_stage = "readiness",
        trust_services_categories = new[] { "security" },
        entries = new[]
        {
            new
            {
                entry_id = Guid.NewGuid(),
                kind = "inclusion",
                subject_type = "application",
                subject = "Payroll",
                governed_record_id = applicationId,
                owner_reference = "Operations",
                rationale = "Declared in scope for the projection spike.",
                unresolved = false,
            },
        },
    };

    static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();
}
