using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class MemberAccessE2ETests(BrokerStackFixture broker) : IClassFixture<BrokerStackFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldExplainSelectedRoleGivenAcceptedMemberInvitation(bool splitHosts)
    {
        // Arrange
        var applicationName = $"compliance-member-access-{Guid.NewGuid():N}";
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
            await using var factory = E2EAppFactory.Create(broker, applicationName);
            var priorMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
            HttpClient administrator;
            HttpClient invitee;
            try
            {
                if (splitHosts)
                    Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", "api");
                administrator = factory.CreateClient();
                invitee = factory.CreateClient();
            }
            finally
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", priorMode);
            }
            using (administrator)
            using (invitee)
            {
                var adminEmail = $"member-admin-{Guid.NewGuid():N}@example.com";
                var inviteeEmail = $"member-invitee-{Guid.NewGuid():N}@example.com";
                await TenantInvitationE2ETests.LoginAsync(administrator, adminEmail);
                var inviteeId = await TenantInvitationE2ETests.LoginAsync(invitee, inviteeEmail);
                using var registered = await administrator.PostAsJsonAsync("/api/v1/tenants", new
                {
                    name = "Member access tenant",
                    slug = $"member-access-{Guid.NewGuid():N}"[..24],
                });
                Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
                var tenant = await registered.Content.ReadFromJsonAsync<TenantDocument>();
                Assert.NotNull(tenant);
                var tenantId = Uuid.Parse(tenant.TenantId, CultureInfo.InvariantCulture);
                var accessPath = $"/api/v1/tenants/{tenantId}/members/{inviteeId}/access";
                var expectedAccessPath = $"{accessPath}?expected_built_in_role=" +
                    BuiltInRbac.ComplianceManagementRole;
                var invitePath = $"/api/v1/tenants/{tenantId}/member-invitations";
                var invitationStatusPath = $"{invitePath}?email_address=" +
                    Uri.EscapeDataString(inviteeEmail);
                var administratorDeadline = DateTimeOffset.UtcNow.AddSeconds(45);
                var adminAccess = HttpStatusCode.Forbidden;
                while (DateTimeOffset.UtcNow < administratorDeadline)
                {
                    using var response = await administrator.GetAsync(
                        $"/api/v1/tenants/{tenantId}/teams/{BuiltInRbac.AdministratorsTeamId(tenantId)}");
                    adminAccess = response.StatusCode;
                    if (adminAccess == HttpStatusCode.OK)
                        break;
                    await Task.Delay(250);
                }
                Assert.Equal(HttpStatusCode.OK, adminAccess);
                using var beforeInvitation = await administrator.GetAsync(invitationStatusPath);
                Assert.Equal(HttpStatusCode.OK, beforeInvitation.StatusCode);
                Assert.Empty((await beforeInvitation.Content.ReadFromJsonAsync<InvitationPageDocument>())!.Items);

                // Act
                using var deniedInvite = await invitee.PostAsJsonAsync(invitePath, new
                {
                    email_address = inviteeEmail,
                    built_in_role = BuiltInRbac.ComplianceManagementRole,
                });
                using var invalidRole = await administrator.PostAsJsonAsync(invitePath, new
                {
                    email_address = inviteeEmail,
                    built_in_role = "unapproved_role",
                });
                using var invited = await administrator.PostAsJsonAsync(invitePath, new
                {
                    email_address = inviteeEmail,
                    built_in_role = BuiltInRbac.ComplianceManagementRole,
                });

                // Assert
                Assert.Equal(HttpStatusCode.NotFound, deniedInvite.StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, invalidRole.StatusCode);
                Assert.Equal(HttpStatusCode.NoContent, invited.StatusCode);
                using var deniedStatus = await invitee.GetAsync(invitationStatusPath);
                Assert.Equal(HttpStatusCode.NotFound, deniedStatus.StatusCode);
                InvitationPageDocument? pendingInvitations = null;
                var invitationDeadline = DateTimeOffset.UtcNow.AddSeconds(45);
                while (DateTimeOffset.UtcNow < invitationDeadline)
                {
                    using var response = await administrator.GetAsync(invitationStatusPath);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    pendingInvitations = await response.Content.ReadFromJsonAsync<InvitationPageDocument>();
                    if (pendingInvitations?.Items.SingleOrDefault()?.Status == "pending")
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
                        break;
                    }
                    await Task.Delay(250);
                }
                Assert.Equal("pending", Assert.Single(pendingInvitations!.Items).Status);
                var delivery = factory.Services.GetRequiredService<MockTenantInvitationDelivery>();
                string? token = null;
                var deliveryDeadline = DateTimeOffset.UtcNow.AddSeconds(45);
                while (DateTimeOffset.UtcNow < deliveryDeadline &&
                       !delivery.TryGetLatest(tenantId, inviteeEmail, out token))
                    await Task.Delay(250);
                Assert.NotNull(token);
                await TenantInvitationE2ETests.VerifyEmailAsync(factory, invitee,
                    inviteeId, inviteeEmail,
                    splitHosts ? worker!.Services.GetRequiredService<MockEmailChallengeDelivery>() : null);
                using var accepted = await invitee.PostAsJsonAsync(
                    $"/api/v1/tenants/{tenantId}/invitations/acceptance",
                    new { email_address = inviteeEmail, token });
                Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);

                MemberAccessDocument? explanation = null;
                var accessDeadline = DateTimeOffset.UtcNow.AddSeconds(45);
                while (DateTimeOffset.UtcNow < accessDeadline)
                {
                    using var response = await administrator.GetAsync(expectedAccessPath);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        explanation = await response.Content.ReadFromJsonAsync<MemberAccessDocument>();
                        if (explanation?.EffectivePermissions.Contains(RbacPermissions.ProgramManage) == true)
                            break;
                    }
                    else
                        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict,
                            await response.Content.ReadAsStringAsync());
                    await Task.Delay(250);
                }
                Assert.NotNull(explanation);
                Assert.Equal(inviteeId, explanation.UserId);
                Assert.Contains(RbacPermissions.TenantAccess, explanation.EffectivePermissions);
                Assert.DoesNotContain(RbacPermissions.TenantRbacManage,
                    explanation.EffectivePermissions);
                Assert.Contains(explanation.Paths, path =>
                    path.TeamName == BuiltInRbac.PowerUsersTeamName &&
                    path.RoleName == BuiltInRbac.ComplianceManagementRoleName);
                InvitationPageDocument? activeInvitations = null;
                var activationDeadline = DateTimeOffset.UtcNow.AddSeconds(45);
                while (DateTimeOffset.UtcNow < activationDeadline)
                {
                    using var response = await administrator.GetAsync(invitationStatusPath);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    activeInvitations = await response.Content.ReadFromJsonAsync<InvitationPageDocument>();
                    if (activeInvitations?.Items.SingleOrDefault()?.Status == "active")
                        break;
                    await Task.Delay(250);
                }
                Assert.Equal("active", Assert.Single(activeInvitations!.Items).Status);
                Assert.Equal(inviteeId, activeInvitations.Items[0].AcceptedUserId);
                using var deniedRead = await invitee.GetAsync(accessPath);
                using var missingRead = await administrator.GetAsync(
                    $"/api/v1/tenants/{tenantId}/members/{Uuid.CreateVersion4()}/access");
                Assert.Equal(HttpStatusCode.Forbidden, deniedRead.StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, missingRead.StatusCode);
                using var invalidExpectation = await administrator.GetAsync(
                    $"{accessPath}?expected_built_in_role=unapproved_role");
                Assert.Equal(HttpStatusCode.BadRequest, invalidExpectation.StatusCode);

                await using var mcp = await McpScenario.ConnectAsync(administrator,
                    new Uri(administrator.BaseAddress!, "/mcp"));
                _ = await mcp.When("bdgrz.member.access.get", new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantId,
                    ["user_id"] = Uuid.Parse(inviteeId, CultureInfo.InvariantCulture),
                    ["expected_built_in_role"] = BuiltInRbac.ComplianceManagementRole,
                }).ExpectSuccess();
                _ = await mcp.When("bdgrz.tenant-invitation.list", new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantId,
                    ["email_address"] = inviteeEmail,
                }).ExpectSuccess();
            }
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

    sealed record TenantDocument([property: JsonPropertyName("tenant_id")] string TenantId);

    sealed record MemberAccessDocument(
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("paths")] IReadOnlyList<MemberAccessPathDocument> Paths,
        [property: JsonPropertyName("effective_permissions")]
        IReadOnlyList<string> EffectivePermissions);

    sealed record MemberAccessPathDocument(
        [property: JsonPropertyName("team_name")] string TeamName,
        [property: JsonPropertyName("role_name")] string RoleName);

    sealed record InvitationPageDocument(
        [property: JsonPropertyName("items")] IReadOnlyList<InvitationDocument> Items);

    sealed record InvitationDocument(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("accepted_user_id")] string? AcceptedUserId);
}
