using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ProgramE2ETests(BrokerStackFixture broker)
{
    [Fact]
    public async Task ShouldProjectProgramChangesGivenIndependentWorker()
    {
        // Arrange
        var applicationName = $"compliance-program-split-{Guid.NewGuid():N}";
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
        using var worker = builder.Build();

        // Act
        await worker.StartAsync();

        // Assert
        try
        {
            await using var factory = E2EAppFactory.Create(broker, applicationName);
            var priorMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
            HttpClient owner;
            try
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", "api");
                owner = factory.CreateClient();
            }
            finally
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", priorMode);
            }
            using (owner)
            {
                await TenantInvitationE2ETests.LoginAsync(owner,
                    $"program-split-owner-{Guid.NewGuid():N}@example.com");
                using var tenantResponse = await owner.PostAsJsonAsync("/api/v1/tenants", new
                {
                    name = "Split Program",
                    slug = $"split-program-{Guid.NewGuid():N}"[..24],
                });
                Assert.Equal(HttpStatusCode.OK, tenantResponse.StatusCode);
                var tenant = await tenantResponse.Content.ReadFromJsonAsync<TenantRegistrationDocument>();
                Assert.NotNull(tenant);
                var path = $"/api/v1/tenants/{tenant.TenantId}/programs";
                var plan = new
                {
                    target_readiness_date = "2027-01-31",
                    target_type_i_as_of_date = "2027-03-31",
                    target_type_ii_start_date = "2027-04-01",
                    target_type_ii_end_date = "2028-03-31",
                    readiness_advisor = "Advisor A",
                    audit_firm = (string?)null,
                };
                ProgramRegistrationDocument? registration = null;
                var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
                while (DateTimeOffset.UtcNow < deadline)
                {
                    using var response = await owner.PostAsJsonAsync(path,
                        new { name = "Split program", plan });
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        registration = await response.Content.ReadFromJsonAsync<ProgramRegistrationDocument>();
                        break;
                    }
                    Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                        await response.Content.ReadAsStringAsync());
                    await Task.Delay(250);
                }
                Assert.NotNull(registration);
                var programPath = $"{path}/{registration.ProgramId}";
                ProgramDocument? projected = null;
                while (DateTimeOffset.UtcNow < deadline)
                {
                    using var response = await owner.GetAsync(programPath);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        projected = await response.Content.ReadFromJsonAsync<ProgramDocument>();
                        if (projected?.Revision == 1)
                            break;
                    }
                    await Task.Delay(250);
                }
                Assert.Equal("Split program", projected?.Name);
                using var revised = await owner.PutAsJsonAsync(programPath,
                    new { expected_revision = 1, name = "Split program revised", plan });
                Assert.Equal(HttpStatusCode.NoContent, revised.StatusCode);
                while (DateTimeOffset.UtcNow < deadline)
                {
                    using var response = await owner.GetAsync(programPath);
                    projected = await response.Content.ReadFromJsonAsync<ProgramDocument>();
                    if (projected?.Revision == 2)
                        break;
                    await Task.Delay(250);
                }
                Assert.Equal("Split program revised", projected?.Name);
                Assert.Equal(2, projected?.Revision);
            }
        }
        finally
        {
            await worker.StopAsync();
        }
    }

    [Fact]
    public async Task ShouldKeepProgramChangesScopedGivenTwoTenants()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var owner = factory.CreateClient();
        using var outsider = factory.CreateClient();
        await TenantInvitationE2ETests.LoginAsync(owner, $"program-owner-{Guid.NewGuid():N}@example.com");
        await TenantInvitationE2ETests.LoginAsync(outsider, $"program-outsider-{Guid.NewGuid():N}@example.com");

        // Act
        using var tenantResponse = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Program E2E",
            slug = $"program-{Guid.NewGuid():N}"[..24],
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, tenantResponse.StatusCode);
        var tenant = await tenantResponse.Content.ReadFromJsonAsync<TenantRegistrationDocument>();
        Assert.NotNull(tenant);
        var tenantId = Uuid.Parse(tenant.TenantId, CultureInfo.InvariantCulture);
        var path = $"/api/v1/tenants/{tenantId}/programs";
        var create = new
        {
            name = "SOC 2 program",
            plan = new
            {
                target_readiness_date = "2027-01-31",
                target_type_i_as_of_date = "2027-03-31",
                target_type_ii_start_date = "2027-04-01",
                target_type_ii_end_date = "2028-03-31",
                readiness_advisor = "Advisor A",
                audit_firm = (string?)null,
            },
        };
        ProgramRegistrationDocument? registration = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(path, create);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                registration = await response.Content.ReadFromJsonAsync<ProgramRegistrationDocument>();
                break;
            }
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        Assert.NotNull(registration);
        var programPath = $"{path}/{registration.ProgramId}";

        ProgramDocument? projected = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(programPath);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                projected = await response.Content.ReadFromJsonAsync<ProgramDocument>();
                if (projected?.Revision == 1)
                    break;
            }
            await Task.Delay(250);
        }
        Assert.Equal("SOC 2 program", projected?.Name);
        Assert.Equal("readiness", projected?.Stage);
        Assert.Equal("type_i", projected?.NextStage);
        Assert.Equal(["readiness", "type_i", "type_ii"],
            projected?.StagePlan.Select(stage => stage.Stage));
        Assert.Equal("Advisor A", projected?.Plan.ReadinessAdvisor);

        using var denied = await outsider.GetAsync(programPath);
        using var missing = await outsider.GetAsync($"{path}/{Uuid.CreateVersion4()}");
        using var deniedList = await outsider.GetAsync(path);
        using var deniedCreate = await outsider.PostAsJsonAsync(path, create);
        using var deniedRevise = await outsider.PutAsJsonAsync(programPath, new
        {
            expected_revision = 1,
            name = "Unauthorized change",
            plan = create.plan,
        });
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(denied.StatusCode, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedList.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedCreate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedRevise.StatusCode);

        using var revised = await owner.PutAsJsonAsync(programPath, new
        {
            expected_revision = 1,
            name = "SOC 2 continuing program",
            plan = new
            {
                target_readiness_date = "2027-02-15",
                target_type_i_as_of_date = "2027-03-31",
                target_type_ii_start_date = "2027-04-01",
                target_type_ii_end_date = "2028-03-31",
                readiness_advisor = "Advisor B",
                audit_firm = (string?)null,
            },
        });
        Assert.Equal(HttpStatusCode.NoContent, revised.StatusCode);
        using var stale = await owner.PutAsJsonAsync(programPath, new
        {
            expected_revision = 1,
            name = "Stale program",
            plan = create.plan,
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(programPath);
            projected = await response.Content.ReadFromJsonAsync<ProgramDocument>();
            if (projected?.Revision == 2)
                break;
            await Task.Delay(250);
        }
        Assert.Equal(2, projected?.Revision);
        Assert.Equal("SOC 2 continuing program", projected?.Name);
        Assert.Equal("Advisor B", projected?.Plan.ReadinessAdvisor);
        using var revisionsResponse = await owner.GetAsync($"{programPath}/revisions");
        Assert.Equal(HttpStatusCode.OK, revisionsResponse.StatusCode);
        var revisions = await revisionsResponse.Content.ReadFromJsonAsync<ProgramRevisionPageDocument>();
        Assert.Equal([1L, 2L], revisions?.Items.Select(item => item.Revision));
        Assert.Equal("Advisor A", revisions?.Items[0].Plan.ReadinessAdvisor);
        Assert.Equal("Advisor B", revisions?.Items[1].Plan.ReadinessAdvisor);
        Assert.All(revisions?.Items ?? [], item => Assert.False(string.IsNullOrWhiteSpace(item.ActorDisplay)));
        using var firstRevisionPage = await owner.GetAsync($"{programPath}/revisions?limit=1");
        var firstRevision = await firstRevisionPage.Content.ReadFromJsonAsync<ProgramRevisionPageDocument>();
        Assert.Equal(1, Assert.Single(firstRevision?.Items ?? []).Revision);
        Assert.False(string.IsNullOrWhiteSpace(firstRevision?.NextCursor));
        using var nextRevisionPage = await owner.GetAsync(
            $"{programPath}/revisions?limit=1&cursor={Uri.EscapeDataString(firstRevision.NextCursor)}");
        var nextRevision = await nextRevisionPage.Content.ReadFromJsonAsync<ProgramRevisionPageDocument>();
        Assert.Equal(2, Assert.Single(nextRevision?.Items ?? []).Revision);
        using var deniedRevisions = await outsider.GetAsync($"{programPath}/revisions");
        Assert.Equal(HttpStatusCode.NotFound, deniedRevisions.StatusCode);
        using var listed = await owner.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        var page = await listed.Content.ReadFromJsonAsync<ProgramPageDocument>();
        Assert.Single(page?.Items ?? []);

        using var secondTenantResponse = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Second Program Tenant",
            slug = $"second-program-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, secondTenantResponse.StatusCode);
        var secondTenant = await secondTenantResponse.Content
            .ReadFromJsonAsync<TenantRegistrationDocument>();
        Assert.NotNull(secondTenant);
        var secondPath = $"/api/v1/tenants/{secondTenant.TenantId}/programs";
        ProgramRegistrationDocument? secondRegistration = null;
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(secondPath,
                new { name = "Second tenant program", plan = create.plan });
            if (response.StatusCode == HttpStatusCode.OK)
            {
                secondRegistration = await response.Content
                    .ReadFromJsonAsync<ProgramRegistrationDocument>();
                break;
            }
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        Assert.NotNull(secondRegistration);
        ProgramPageDocument? firstTenantPrograms = null;
        ProgramPageDocument? secondTenantPrograms = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var firstList = await owner.GetAsync(path);
            using var secondList = await owner.GetAsync(secondPath);
            firstTenantPrograms = await firstList.Content.ReadFromJsonAsync<ProgramPageDocument>();
            secondTenantPrograms = await secondList.Content.ReadFromJsonAsync<ProgramPageDocument>();
            if (firstTenantPrograms?.Items.Count == 1 && secondTenantPrograms?.Items.Count == 1)
                break;
            await Task.Delay(250);
        }
        Assert.Equal("SOC 2 continuing program", Assert.Single(firstTenantPrograms?.Items ?? []).Name);
        Assert.Equal("Second tenant program", Assert.Single(secondTenantPrograms?.Items ?? []).Name);

        using var conflictingTenant = await owner.PostAsJsonAsync(path, new
        {
            tenant_id = secondTenant.TenantId,
            name = "Conflicting tenant body",
            plan = create.plan,
        });
        Assert.Equal(HttpStatusCode.OK, conflictingTenant.StatusCode);
        ProgramPageDocument? firstAfterConflict = null;
        ProgramPageDocument? secondAfterConflict = null;
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var firstList = await owner.GetAsync(path);
            using var secondList = await owner.GetAsync(secondPath);
            firstAfterConflict = await firstList.Content.ReadFromJsonAsync<ProgramPageDocument>();
            secondAfterConflict = await secondList.Content.ReadFromJsonAsync<ProgramPageDocument>();
            if (firstAfterConflict?.Items.Count == 2 || secondAfterConflict?.Items.Count == 2)
                break;
            await Task.Delay(250);
        }
        Assert.Equal(2, firstAfterConflict?.Items.Count);
        Assert.Single(secondAfterConflict?.Items ?? []);
    }

    sealed record TenantRegistrationDocument([property: JsonPropertyName("tenant_id")] string TenantId);
    sealed record ProgramRegistrationDocument([property: JsonPropertyName("program_id")] string ProgramId);
    sealed record ProgramPlanDocument([property: JsonPropertyName("readiness_advisor")] string? ReadinessAdvisor);
    sealed record ProgramDocument(string Name, string Stage,
        [property: JsonPropertyName("next_stage")] string? NextStage, long Revision,
        ProgramPlanDocument Plan,
        [property: JsonPropertyName("stage_plan")] IReadOnlyList<ProgramStageDocument> StagePlan);
    sealed record ProgramStageDocument(string Stage,
        [property: JsonPropertyName("advance_when")] string AdvanceWhen);
    sealed record ProgramPageDocument(IReadOnlyList<ProgramDocument> Items);
    sealed record ProgramRevisionPageDocument(IReadOnlyList<ProgramRevisionDocument> Items,
        [property: JsonPropertyName("next_cursor")] string? NextCursor);
    sealed record ProgramRevisionDocument(long Revision, ProgramPlanDocument Plan,
        [property: JsonPropertyName("actor_display")] string ActorDisplay);
}
