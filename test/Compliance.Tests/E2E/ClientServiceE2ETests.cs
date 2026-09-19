using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ClientServiceE2ETests(BrokerStackFixture broker)
{
    [Fact]
    public async Task ShouldPreserveServiceHistoryAndDenyOutsiderGivenBrokerHost()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var owner = factory.CreateClient();
        using var outsider = factory.CreateClient();
        await TenantInvitationE2ETests.LoginAsync(owner,
            $"service-owner-{Guid.NewGuid():N}@example.com");
        await TenantInvitationE2ETests.LoginAsync(outsider,
            $"service-outsider-{Guid.NewGuid():N}@example.com");

        // Act
        using var tenantResponse = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Service tenant",
            slug = $"service-{Guid.NewGuid():N}"[..24],
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, tenantResponse.StatusCode);
        var tenant = await tenantResponse.Content.ReadFromJsonAsync<TenantDocument>();
        Assert.NotNull(tenant);
        var programsPath = $"/api/v1/tenants/{tenant.TenantId}/programs";
        ProgramRegistrationDocument? program = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(programsPath, new
            {
                name = "Service program",
                plan = new
                {
                    target_readiness_date = "2027-01-31",
                    target_type_i_as_of_date = "2027-03-31",
                    target_type_ii_start_date = "2027-04-01",
                    target_type_ii_end_date = "2028-03-31",
                    readiness_advisor = "Advisor",
                    audit_firm = (string?)null,
                },
            });
            if (response.StatusCode == HttpStatusCode.OK)
            {
                program = await response.Content.ReadFromJsonAsync<ProgramRegistrationDocument>();
                break;
            }
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        Assert.NotNull(program);
        var path = $"/api/v1/tenants/{tenant.TenantId}/client-services";
        var createPath = $"{programsPath}/{program.ProgramId}/client-services";
        ServiceRegistrationDocument? service = null;
        using var missingProgram = await owner.PostAsJsonAsync(
            $"{programsPath}/{Guid.NewGuid()}/client-services", new
            {
                name = "Unknown",
                purpose = "Unknown",
                owner_reference = "Operations",
            });
        Assert.Equal(HttpStatusCode.NotFound, missingProgram.StatusCode);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(createPath, new
            {
                name = "Payroll",
                purpose = "Run payroll",
                owner_reference = "Operations",
            });
            if (response.StatusCode == HttpStatusCode.OK)
            {
                service = await response.Content.ReadFromJsonAsync<ServiceRegistrationDocument>();
                break;
            }
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        Assert.NotNull(service);
        var servicePath = $"{path}/{service.ServiceId}";
        ServiceDocument? projected = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(servicePath);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                projected = await response.Content.ReadFromJsonAsync<ServiceDocument>();
                if (projected?.Revision == 1)
                    break;
            }
            await Task.Delay(250);
        }
        Assert.Equal("active", projected?.Status);
        Assert.Equal(program.ProgramId, projected?.ProgramId);
        using var programList = await owner.GetAsync(createPath);
        Assert.Equal(HttpStatusCode.OK, programList.StatusCode);
        Assert.Contains(service.ServiceId.ToString(), await programList.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
        var toolInput = new Dictionary<string, object?>
        {
            ["tenant_id"] = tenant.TenantId,
            ["service_id"] = service.ServiceId,
        };
        await using (var ownerMcp = await McpScenario.ConnectAsync(owner,
                         new Uri(owner.BaseAddress!, "/mcp")))
        {
            _ = await ownerMcp.When("bdgrz.client-service.get", toolInput).ExpectSuccess();
        }
        await using (var outsiderMcp = await McpScenario.ConnectAsync(outsider,
                         new Uri(outsider.BaseAddress!, "/mcp")))
        {
            _ = await outsiderMcp.When("bdgrz.client-service.get", toolInput).ExpectFailure();
        }
        using var denied = await outsider.GetAsync(servicePath);
        using var hidden = await outsider.GetAsync($"{path}/{Guid.NewGuid()}");
        using var deniedList = await outsider.GetAsync(path);
        using var deniedProgramList = await outsider.GetAsync(createPath);
        using var deniedCreate = await outsider.PostAsJsonAsync(createPath, new
        {
            name = "Hidden",
            purpose = "Hidden",
            owner_reference = "Outsider",
        });
        using var deniedRevise = await outsider.PutAsJsonAsync(servicePath, new
        {
            expected_revision = 1,
            name = "Hidden",
            purpose = "Hidden",
            owner_reference = "Outsider",
        });
        using var deniedRetire = await outsider.PostAsJsonAsync($"{servicePath}/retirements",
            new { expected_revision = 1, rationale = "Outsider" });
        using var deniedHistory = await outsider.GetAsync($"{servicePath}/revisions");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(hidden.StatusCode, denied.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedList.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedProgramList.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedCreate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedRevise.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedRetire.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedHistory.StatusCode);

        using var changed = await owner.PutAsJsonAsync(servicePath, new
        {
            expected_revision = 1,
            name = "Payroll",
            purpose = "Monthly payroll",
            owner_reference = "Finance",
        });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        using var stale = await owner.PutAsJsonAsync(servicePath, new
        {
            expected_revision = 1,
            name = "Payroll",
            purpose = "Stale",
            owner_reference = "Finance",
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(servicePath);
            projected = await response.Content.ReadFromJsonAsync<ServiceDocument>();
            if (projected?.Revision == 2)
                break;
            await Task.Delay(250);
        }
        Assert.Equal("Monthly payroll", projected?.Purpose);
        using var retired = await owner.PostAsJsonAsync($"{servicePath}/retirements",
            new { expected_revision = 2, rationale = "Service ended" });
        Assert.Equal(HttpStatusCode.NoContent, retired.StatusCode);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(servicePath);
            projected = await response.Content.ReadFromJsonAsync<ServiceDocument>();
            if (projected?.Revision == 3)
                break;
            await Task.Delay(250);
        }
        Assert.Equal("retired", projected?.Status);
        using var history = await owner.GetAsync($"{servicePath}/revisions");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        var revisions = await history.Content.ReadFromJsonAsync<ServiceRevisionPageDocument>();
        Assert.Equal([1L, 2L, 3L], revisions?.Items.Select(item => item.Revision));
        Assert.Equal("Service ended", revisions?.Items[2].RetirementRationale);
    }

    sealed record TenantDocument([property: JsonPropertyName("tenant_id")] string TenantId);
    sealed record ProgramRegistrationDocument([property: JsonPropertyName("program_id")] string ProgramId);
    sealed record ServiceRegistrationDocument([property: JsonPropertyName("service_id")] string ServiceId);
    sealed record ServiceDocument(long Revision, string Purpose, string Status,
        [property: JsonPropertyName("program_id")] string? ProgramId);
    sealed record ServiceRevisionPageDocument(IReadOnlyList<ServiceRevisionDocument> Items);
    sealed record ServiceRevisionDocument(long Revision,
        [property: JsonPropertyName("retirement_rationale")] string? RetirementRationale);
}
