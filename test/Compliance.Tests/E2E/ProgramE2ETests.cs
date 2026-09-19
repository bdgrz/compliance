using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ProgramE2ETests(BrokerStackFixture broker)
{
    [Fact]
    public async Task CreateReviseAndReadProgramRemainScopedToItsTenant()
    {
        await using var factory = E2EAppFactory.Create(broker);
        using var owner = factory.CreateClient();
        using var outsider = factory.CreateClient();
        await TenantInvitationE2ETests.LoginAsync(owner, $"program-owner-{Guid.NewGuid():N}@example.com");
        await TenantInvitationE2ETests.LoginAsync(outsider, $"program-outsider-{Guid.NewGuid():N}@example.com");

        using var tenantResponse = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Program E2E",
            slug = $"program-{Guid.NewGuid():N}"[..24],
        });
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
        using var listed = await owner.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        var page = await listed.Content.ReadFromJsonAsync<ProgramPageDocument>();
        Assert.Single(page?.Items ?? []);
    }

    sealed record TenantRegistrationDocument([property: JsonPropertyName("tenant_id")] string TenantId);
    sealed record ProgramRegistrationDocument([property: JsonPropertyName("program_id")] string ProgramId);
    sealed record ProgramPlanDocument([property: JsonPropertyName("readiness_advisor")] string? ReadinessAdvisor);
    sealed record ProgramDocument(string Name, string Stage,
        [property: JsonPropertyName("next_stage")] string? NextStage, long Revision,
        ProgramPlanDocument Plan);
    sealed record ProgramPageDocument(IReadOnlyList<ProgramDocument> Items);
}
