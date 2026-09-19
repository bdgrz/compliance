using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     Exercises the real permission-grant round trip against a live Fitz broker: register a
///     tenant (which bootstraps built-in teams/roles/permissions through real
///     <see cref="FitzPermissionProjection" /> writes), then confirm the creator can actually read
///     a permission-gated resource. This is precisely the path that silently never worked when
///     <c>PermissionProjectionState.RolePermissions</c> was a <c>Dictionary&lt;Uuid, ...&gt;</c> --
///     every real write threw mid-transaction, so no permission was ever durably granted, and
///     every tenant-scoped request was forbidden forever. See <see cref="PermissionProjectionState" />
///     for that history; this test is what should have caught it before a human had to.
/// </summary>
[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class PermissionGrantE2ETests(BrokerStackFixture broker)
{
    [Fact]
    public async Task ShouldGrantTheCreatorTenantAccessThroughRealRolePermissionMaterialization()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var client = factory.CreateClient();
        await LogInAsDeveloperAsync(client);

        using var registerResponse = await client.PostAsJsonAsync(
            "/api/v1/tenants",
            new { name = "E2E Permission Grant", slug = $"e2e-permission-{Guid.NewGuid():N}"[..24] });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var tenant = await registerResponse.Content.ReadFromJsonAsync<TenantRegistrationDocument>();
        Assert.NotNull(tenant);
        var tenantId = Uuid.Parse(tenant.TenantId, CultureInfo.InvariantCulture);
        var administratorsTeamId = BuiltInRbac.AdministratorsTeamId(tenantId);

        // Act: TenantRbacBootstrapReactor grants the creator TenantAccess asynchronously, through
        // a real write -> real materialize -> real read cycle, so poll rather than assume it has
        // landed by the time RegisterTenant returns.
        HttpResponseMessage? getTeamResponse = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            getTeamResponse?.Dispose();
            getTeamResponse = await client.GetAsync(
                $"/api/v1/tenants/{tenantId}/teams/{administratorsTeamId}", CancellationToken.None);
            if (getTeamResponse.StatusCode == HttpStatusCode.OK)
                break;

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        // Assert
        Assert.NotNull(getTeamResponse);
        Assert.Equal(HttpStatusCode.OK, getTeamResponse.StatusCode);
        var team = await getTeamResponse.Content.ReadFromJsonAsync<TeamDocument>();
        Assert.NotNull(team);
        Assert.Equal(BuiltInRbac.AdministratorsTeamName, team.Name);
    }

    static async Task LogInAsDeveloperAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/developer-user-sessions",
            new RegistrationDocument($"e2e-{Guid.NewGuid():N}@example.com"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    sealed record RegistrationDocument(
        [property: JsonPropertyName("email_address")] string EmailAddress);

    sealed record TenantRegistrationDocument(
        [property: JsonPropertyName("tenant_id")] string TenantId,
        string Slug);

    sealed record TeamDocument(
        [property: JsonPropertyName("team_id")] string TeamId,
        string Name);
}
