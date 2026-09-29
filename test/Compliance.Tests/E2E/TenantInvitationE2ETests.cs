using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class TenantInvitationE2ETests(BrokerStackFixture broker) : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldSuspendAndReinstateMemberGivenAdministratorAndIndependentLifecycleHistory()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker)
            .WithWebHostBuilder(host => host.ConfigureTestServices(services =>
                services.AddSingleton(new PlatformOperatorAuthority([Uuid.CreateVersion4()]))));
        using var administratorClient = factory.CreateClient();
        var administratorEmail = $"administrator-{Guid.NewGuid():N}@example.com";
        var administratorId = await LoginAsync(administratorClient, administratorEmail);
        await VerifyEmailAsync(factory, administratorClient, administratorId, administratorEmail);
        using var registrationResponse = await administratorClient.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Member lifecycle E2E",
            slug = $"member-life-{Guid.NewGuid():N}"[..24],
            legal_name = "Member Lifecycle LLC",
        });
        Assert.Equal(HttpStatusCode.OK, registrationResponse.StatusCode);
        var registration = await registrationResponse.Content.ReadFromJsonAsync<TenantRegistrationDocument>();
        Assert.NotNull(registration);
        var tenantId = Uuid.Parse(registration.TenantId, CultureInfo.InvariantCulture);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        var administratorAccess = HttpStatusCode.Forbidden;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await administratorClient.GetAsync(
                $"/api/v1/tenants/{tenantId}/teams/{BuiltInRbac.AdministratorsTeamId(tenantId)}");
            administratorAccess = response.StatusCode;
            if (administratorAccess == HttpStatusCode.OK)
                break;
            await Task.Delay(250);
        }
        Assert.Equal(HttpStatusCode.OK, administratorAccess);

        var memberEmail = $"member-{Guid.NewGuid():N}@example.com";
        using var invitedMember = await administratorClient.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/invitations",
            new { email_address = memberEmail, affiliation = "client_personnel" });
        Assert.Equal(HttpStatusCode.NoContent, invitedMember.StatusCode);
        var invitationDelivery = factory.Services.GetRequiredService<MockTenantInvitationDelivery>();
        string? memberToken = null;
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline &&
               !invitationDelivery.TryGetLatest(tenantId, memberEmail, out memberToken))
            await Task.Delay(250);
        Assert.NotNull(memberToken);

        using var memberClient = factory.CreateClient();
        var memberUserId = await LoginAsync(memberClient, memberEmail);
        await VerifyEmailAsync(factory, memberClient, memberUserId, memberEmail);
        using var acceptedMember = await memberClient.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/invitations/acceptance",
            new { email_address = memberEmail, token = memberToken });
        Assert.Equal(HttpStatusCode.NoContent, acceptedMember.StatusCode);

        var standardUsersTeamId = BuiltInRbac.StandardUsersTeamId(tenantId);
        var memberId = RbacIds.Member(tenantId, Uuid.Parse(memberUserId, CultureInfo.InvariantCulture));
        using var assignedMember = await administratorClient.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/teams/{standardUsersTeamId}/members/{memberId}", new { });
        Assert.Equal(HttpStatusCode.NoContent, assignedMember.StatusCode);
        var memberTeamPath = $"/api/v1/tenants/{tenantId}/teams/{standardUsersTeamId}";
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        var memberAccess = HttpStatusCode.Forbidden;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await memberClient.GetAsync(memberTeamPath);
            memberAccess = response.StatusCode;
            if (memberAccess == HttpStatusCode.OK)
                break;
            await Task.Delay(250);
        }
        Assert.Equal(HttpStatusCode.OK, memberAccess);
        await using var memberMcp = await McpScenario.ConnectAsync(memberClient,
            new Uri(memberClient.BaseAddress!, "/mcp"));
        var teamArguments = new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId.ToString(),
            ["team_id"] = standardUsersTeamId.ToString(),
        };
        _ = await memberMcp.When("bdgrz.rbac.team.get", teamArguments).ExpectSuccess();

        var boundaryId = Uuid.CreateVersion4();
        var boundaryVersionId = Uuid.CreateVersion4();
        var boundary = new SystemBoundary(tenantId, boundaryId);
        Assert.True(boundary.Create(Uuid.CreateVersion4(), boundaryVersionId,
            new BoundaryContent("Member responsibility", "readiness", ["security"], []),
            Uuid.Parse(administratorId, CultureInfo.InvariantCulture), "Administrator",
            DateTimeOffset.UtcNow).IsSuccess);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IAggregateWriter>().SaveAsync(
                boundary, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        using var assignedResponsibility = await administratorClient.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/responsibilities", new
            {
                member_user_id = memberUserId,
                type = "control_owner",
                record_type = "boundary",
                record_id = boundaryId,
                version_id = boundaryVersionId,
                scope_revision = 1,
                effective_from = DateTimeOffset.UtcNow.AddMinutes(-1),
                effective_until = (DateTimeOffset?)null,
                separation_of_duties_waiver_ids = Array.Empty<string>(),
            });
        Assert.Equal(HttpStatusCode.NoContent, assignedResponsibility.StatusCode);
        var orphanedWorkPath = $"/api/v1/tenants/{tenantId}/members/{memberUserId}/responsibilities";
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        JsonElement openAssignments = default;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await administratorClient.GetAsync(orphanedWorkPath);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            openAssignments = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (openAssignments.ValueKind == JsonValueKind.Array && openAssignments.GetArrayLength() == 1)
                break;
            await Task.Delay(250);
        }
        Assert.Equal(1, openAssignments.GetArrayLength());
        var assignmentId = openAssignments[0].GetProperty("assignment_id").GetString();
        Assert.NotNull(assignmentId);

        // Act
        using var selfSuspension = await administratorClient.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/members/{administratorId}/suspensions",
            new { reason = "Self suspension must be rejected." });
        Assert.Equal(HttpStatusCode.Conflict, selfSuspension.StatusCode);
        using var suspended = await administratorClient.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/members/{memberUserId}/suspensions",
            new { reason = "Access review is overdue." });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, suspended.StatusCode);
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        MemberLifecycleDocument? membership = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await administratorClient.GetAsync(
                $"/api/v1/tenants/{tenantId}/members/{memberUserId}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                membership = await response.Content.ReadFromJsonAsync<MemberLifecycleDocument>();
                if (membership?.IsSuspended == true)
                    break;
            }
            await Task.Delay(250);
        }
        Assert.NotNull(membership);
        Assert.True(membership.IsSuspended);
        Assert.Equal("Access review is overdue.", membership.SuspensionReason);
        Assert.Equal(RbacIds.Member(tenantId, Uuid.Parse(administratorId, CultureInfo.InvariantCulture)),
            membership.SuspendedByMemberId);
        using var denied = await memberClient.GetAsync(memberTeamPath);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        _ = await memberMcp.When("bdgrz.rbac.team.get", teamArguments).ExpectFailure();
        using var orphanedWork = await administratorClient.GetAsync(orphanedWorkPath);
        Assert.Equal(HttpStatusCode.OK, orphanedWork.StatusCode);
        var orphanedAssignments = await orphanedWork.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(assignmentId, Assert.Single(orphanedAssignments.EnumerateArray())
            .GetProperty("assignment_id").GetString());
        await using (var readMcp = await McpScenario.ConnectAsync(administratorClient,
                         new Uri(administratorClient.BaseAddress!, "/mcp")))
        {
            _ = await readMcp.When("bdgrz.member.responsibilities.list",
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantId.ToString(),
                    ["user_id"] = memberUserId,
                }).ExpectSuccess();
        }

        using var reinstated = await administratorClient.DeleteAsync(
            $"/api/v1/tenants/{tenantId}/members/{memberUserId}/suspensions");
        Assert.Equal(HttpStatusCode.NoContent, reinstated.StatusCode);
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        memberAccess = HttpStatusCode.Forbidden;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await memberClient.GetAsync(memberTeamPath);
            memberAccess = response.StatusCode;
            if (memberAccess == HttpStatusCode.OK)
                break;
            await Task.Delay(250);
        }
        Assert.Equal(HttpStatusCode.OK, memberAccess);
        _ = await memberMcp.When("bdgrz.rbac.team.get", teamArguments).ExpectSuccess();
        MemberLifecycleDocument? reinstatedMembership = null;
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var readback = await administratorClient.GetAsync(
                $"/api/v1/tenants/{tenantId}/members/{memberUserId}");
            if (readback.StatusCode == HttpStatusCode.OK)
            {
                reinstatedMembership = await readback.Content.ReadFromJsonAsync<MemberLifecycleDocument>();
                if (reinstatedMembership is { IsSuspended: false, ReinstatedAt: not null })
                    break;
            }
            await Task.Delay(250);
        }
        Assert.NotNull(reinstatedMembership);
        Assert.False(reinstatedMembership.IsSuspended);
        Assert.NotNull(reinstatedMembership.ReinstatedAt);
        Assert.Equal(RbacIds.Member(tenantId, Uuid.Parse(administratorId, CultureInfo.InvariantCulture)),
            reinstatedMembership.ReinstatedByMemberId);

        // Run the same lifecycle through MCP, with the member still using HTTP and MCP to
        // prove that both surfaces consult the current authorization state.
        await using var administratorMcp = await McpScenario.ConnectAsync(administratorClient,
            new Uri(administratorClient.BaseAddress!, "/mcp"));
        var memberArguments = new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId.ToString(),
            ["user_id"] = memberUserId,
        };
        _ = await administratorMcp.When("bdgrz.tenant.member.suspend",
            new Dictionary<string, object?>(memberArguments)
            {
                ["reason"] = "MCP access review is overdue.",
            }).ExpectSuccess();
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        MemberLifecycleDocument? mcpSuspendedMembership = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var readback = await administratorClient.GetAsync(
                $"/api/v1/tenants/{tenantId}/members/{memberUserId}");
            Assert.Equal(HttpStatusCode.OK, readback.StatusCode);
            mcpSuspendedMembership = await readback.Content.ReadFromJsonAsync<MemberLifecycleDocument>();
            if (mcpSuspendedMembership?.IsSuspended == true)
                break;
            await Task.Delay(250);
        }
        Assert.True(mcpSuspendedMembership?.IsSuspended);
        using (var deniedAfterMcp = await memberClient.GetAsync(memberTeamPath))
            Assert.Equal(HttpStatusCode.NotFound, deniedAfterMcp.StatusCode);
        _ = await memberMcp.When("bdgrz.rbac.team.get", teamArguments).ExpectFailure();
        _ = await administratorMcp.When("bdgrz.tenant.member.get", memberArguments).ExpectSuccess();
        _ = await administratorMcp.When("bdgrz.tenant.member.reinstate", memberArguments).ExpectSuccess();
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        memberAccess = HttpStatusCode.Forbidden;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await memberClient.GetAsync(memberTeamPath);
            memberAccess = response.StatusCode;
            if (memberAccess == HttpStatusCode.OK)
                break;
            await Task.Delay(250);
        }
        Assert.Equal(HttpStatusCode.OK, memberAccess);
        _ = await memberMcp.When("bdgrz.rbac.team.get", teamArguments).ExpectSuccess();
    }

    [Fact]
    public async Task ShouldGrantTenantAccessGivenVerifiedAcceptedAdministratorInvitation()
    {
        // Arrange
        var activationLag = new TransientTenantAccessPermissionLag();
        await using var factory = E2EAppFactory.Create(broker)
            .WithWebHostBuilder(host => host.ConfigureTestServices(services =>
                TransientTenantAccessPermissionTestRegistration.Install(services, activationLag)));
        using var operatorClient = factory.CreateClient();
        using var administratorClient = factory.CreateClient();
        var operatorEmail = $"operator-{Guid.NewGuid():N}@example.com";
        var administratorEmail = $"admin-{Guid.NewGuid():N}@example.com";
        await LoginAsync(operatorClient, operatorEmail);

        // Act
        using var registered = await operatorClient.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Invitation E2E",
            slug = $"invite-{Guid.NewGuid():N}"[..24],
            legal_name = "Invitation E2E Legal LLC",
            first_administrator_email = administratorEmail,
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        var registration = await registered.Content.ReadFromJsonAsync<TenantRegistrationDocument>();
        Assert.NotNull(registration);
        var tenantId = Uuid.Parse(registration.TenantId, CultureInfo.InvariantCulture);
        var administratorsTeamId = BuiltInRbac.AdministratorsTeamId(tenantId);
        using var operatorDenied = await operatorClient.GetAsync(
            $"/api/v1/tenants/{tenantId}/teams/{administratorsTeamId}");
        Assert.Equal(HttpStatusCode.NotFound, operatorDenied.StatusCode);

        var invitationDelivery = factory.Services.GetRequiredService<MockTenantInvitationDelivery>();
        string? invitationToken = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline &&
               !invitationDelivery.TryGetLatest(tenantId, administratorEmail, out invitationToken))
            await Task.Delay(250);
        Assert.NotNull(invitationToken);

        var administratorId = await LoginAsync(administratorClient, administratorEmail);
        activationLag.TargetUser(administratorId);
        var acceptance = $"/api/v1/tenants/{tenantId}/invitations/acceptance";
        using var unverified = await administratorClient.PostAsJsonAsync(acceptance,
            new { email_address = administratorEmail, token = invitationToken });
        Assert.Equal(HttpStatusCode.Forbidden, unverified.StatusCode);
        await VerifyEmailAsync(factory, administratorClient, administratorId, administratorEmail);

        using var invalid = await administratorClient.PostAsJsonAsync(acceptance,
            new { email_address = administratorEmail, token = "wrong-token" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var accepted = await administratorClient.PostAsJsonAsync(acceptance,
            new { email_address = administratorEmail, token = invitationToken });
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);

        using var activationDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await activationLag.WaitForFirstFailureAsync(activationDeadline.Token);
        using (var deniedDuringLag = await administratorClient.GetAsync(
                   $"/api/v1/tenants/{tenantId}/teams/{administratorsTeamId}"))
            Assert.Equal(HttpStatusCode.Forbidden, deniedDuringLag.StatusCode);
        Assert.True(activationLag.FailureCount >= 1);
        activationLag.Release();

        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        var access = HttpStatusCode.Forbidden;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await administratorClient.GetAsync(
                $"/api/v1/tenants/{tenantId}/teams/{administratorsTeamId}");
            access = response.StatusCode;
            if (access == HttpStatusCode.OK)
                break;
            await Task.Delay(250);
        }
        Assert.Equal(HttpStatusCode.OK, access);

        using var administratorTenant = await administratorClient.GetAsync($"/api/v1/tenants/{tenantId}");
        Assert.Equal(HttpStatusCode.OK, administratorTenant.StatusCode);
        using var otherTenant = await administratorClient.GetAsync($"/api/v1/tenants/{Uuid.CreateVersion4()}");
        Assert.Equal(HttpStatusCode.NotFound, otherTenant.StatusCode);

        using var tenantResponse = await operatorClient.GetAsync($"/api/v1/tenants/{tenantId}");
        Assert.Equal(HttpStatusCode.OK, tenantResponse.StatusCode);
        var tenant = await tenantResponse.Content.ReadFromJsonAsync<TenantDocument>();
        Assert.Equal("Invitation E2E Legal LLC", tenant?.LegalName);
        Assert.Equal("active", tenant?.Status);

        var staffEmail = $"staff-{Guid.NewGuid():N}@example.com";
        using var invitedStaff = await operatorClient.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/invitations",
            new { email_address = staffEmail, affiliation = "firm_staff", administrator = false });
        Assert.Equal(HttpStatusCode.NoContent, invitedStaff.StatusCode);
        string? staffToken = null;
        deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline &&
               !invitationDelivery.TryGetLatest(tenantId, staffEmail, out staffToken))
            await Task.Delay(250);
        Assert.NotNull(staffToken);

        using var staffClient = factory.CreateClient();
        var staffId = await LoginAsync(staffClient, staffEmail);
        await VerifyEmailAsync(factory, staffClient, staffId, staffEmail);
        using var acceptedStaff = await staffClient.PostAsJsonAsync(acceptance,
            new { email_address = staffEmail, token = staffToken });
        Assert.Equal(HttpStatusCode.NoContent, acceptedStaff.StatusCode);

        TenantMemberListDocument? members = null;
        deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await operatorClient.GetAsync($"/api/v1/tenants/{tenantId}/members");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                members = await response.Content.ReadFromJsonAsync<TenantMemberListDocument>();
                if (members?.Items.Count == 2)
                    break;
            }
            await Task.Delay(250);
        }
        Assert.NotNull(members);
        Assert.Equal(2, members.Items.Count);
        Assert.Contains(members.Items, item => item.UserId == staffId && item.Affiliation == "firm_staff");
        Assert.Contains(members.Items, item => item.UserId == administratorId &&
            item.Affiliation == "client_personnel");
        using var staffDenied = await staffClient.GetAsync(
            $"/api/v1/tenants/{tenantId}/teams/{administratorsTeamId}");
        Assert.Equal(HttpStatusCode.Forbidden, staffDenied.StatusCode);

        // A historical or accidental team grant must not become standing client access.
        var staffMemberId = RbacIds.Member(tenantId,
            Uuid.Parse(staffId, CultureInfo.InvariantCulture));
        using var assignedStaff = await administratorClient.PostAsync(
            $"/api/v1/tenants/{tenantId}/teams/{administratorsTeamId}/members/{staffMemberId}", null);
        Assert.Equal(HttpStatusCode.NoContent, assignedStaff.StatusCode);
        var projectedAssignment = false;
        deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await administratorClient.GetAsync(
                $"/api/v1/tenants/{tenantId}/teams/{administratorsTeamId}/members");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using var membersDocument = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync());
                projectedAssignment = membersDocument.RootElement.GetProperty("items")
                    .EnumerateArray().Any(member => member.GetProperty("member_id").GetString() ==
                        staffMemberId.ToString());
                if (projectedAssignment)
                    break;
            }
            await Task.Delay(250);
        }
        Assert.True(projectedAssignment);
        using var staffAccessResponse = await administratorClient.GetAsync(
            $"/api/v1/tenants/{tenantId}/members/{staffId}/access");
        Assert.Equal(HttpStatusCode.OK, staffAccessResponse.StatusCode);
        using var accessDocument = await JsonDocument.ParseAsync(
            await staffAccessResponse.Content.ReadAsStreamAsync());
        Assert.Empty(accessDocument.RootElement.GetProperty("effective_permissions").EnumerateArray());
        using var staffStillDenied = await staffClient.GetAsync(
            $"/api/v1/tenants/{tenantId}/teams/{administratorsTeamId}");
        Assert.Equal(HttpStatusCode.Forbidden, staffStillDenied.StatusCode);
        using var staffProgramDenied = await staffClient.GetAsync(
            $"/api/v1/tenants/{tenantId}/programs");
        Assert.Equal(HttpStatusCode.Forbidden, staffProgramDenied.StatusCode);
        await using (var mcp = await McpScenario.ConnectAsync(staffClient,
                         new Uri(staffClient.BaseAddress!, "/mcp")))
            _ = await mcp.When("bdgrz.program.list", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId.ToString(),
            }).ExpectFailure();

        var newSlug = $"renamed-{Guid.NewGuid():N}"[..24];
        using var changed = await operatorClient.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/slug-changes", new { slug = newSlug });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        TenantSlugResolutionDocument? oldLink = null;
        deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await administratorClient.GetAsync(
                $"/api/v1/tenant-slugs/{registration.Slug}/mine");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                oldLink = await response.Content.ReadFromJsonAsync<TenantSlugResolutionDocument>();
                if (oldLink?.CurrentSlug == newSlug && oldLink.Redirect)
                    break;
            }
            await Task.Delay(250);
        }
        Assert.NotNull(oldLink);
        Assert.Equal(newSlug, oldLink?.CurrentSlug);
        Assert.True(oldLink?.Redirect);
        using var unknown = await administratorClient.GetAsync(
            "/api/v1/tenant-slugs/unknown-client/mine");
        using var forbiddenSlug = await operatorClient.GetAsync(
            $"/api/v1/tenant-slugs/{registration.Slug}/mine");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, forbiddenSlug.StatusCode);
        using var reuse = await operatorClient.PostAsJsonAsync("/api/v1/tenants",
            new { name = "Reuse", slug = registration.Slug });
        Assert.Equal(HttpStatusCode.Conflict, reuse.StatusCode);
    }

    internal static async Task<string> LoginAsync(HttpClient client, string emailAddress)
    {
        using var login = await client.PostAsJsonAsync("/api/v1/developer-user-sessions",
            new { email_address = emailAddress });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var session = await client.GetAsync("/auth/session");
        var identity = await session.Content.ReadFromJsonAsync<SessionDocument>();
        Assert.NotNull(identity);
        return identity.Id;
    }

    internal static async Task VerifyEmailAsync(WebApplicationFactory<Program> factory, HttpClient client,
        string userId, string emailAddress, MockEmailChallengeDelivery? workerDelivery = null)
    {
        var path = $"/api/v1/users/{userId}/email-addresses/{emailAddress}";
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
                break;
            await Task.Delay(250);
        }
        using var issued = await client.PostAsync($"{path}/challenges", null);
        Assert.Equal(HttpStatusCode.NoContent, issued.StatusCode);
        var delivery = workerDelivery ?? factory.Services.GetRequiredService<MockEmailChallengeDelivery>();
        string? token = null;
        var deliveryDeadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deliveryDeadline &&
               !delivery.TryGetLatest(Uuid.Parse(userId, CultureInfo.InvariantCulture),
                   emailAddress, out token))
            await Task.Delay(250);
        Assert.NotNull(token);
        using var completed = await client.PostAsJsonAsync($"{path}/verifications", new { token });
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK &&
                (await response.Content.ReadFromJsonAsync<EmailDocument>())?.Verified == true)
                return;
            await Task.Delay(250);
        }
        Assert.Fail("The verified address was not projected.");
    }

    sealed record TenantRegistrationDocument([property: JsonPropertyName("tenant_id")] string TenantId,
        string Slug);
    sealed record MemberLifecycleDocument(
        [property: JsonPropertyName("is_suspended")] bool IsSuspended,
        [property: JsonPropertyName("suspension_reason")] string? SuspensionReason,
        [property: JsonPropertyName("suspended_by_member_id")] Uuid SuspendedByMemberId,
        [property: JsonPropertyName("reinstated_at")] DateTimeOffset? ReinstatedAt,
        [property: JsonPropertyName("reinstated_by_member_id")] Uuid ReinstatedByMemberId);
    sealed record TenantSlugResolutionDocument(
        [property: JsonPropertyName("current_slug")] string CurrentSlug, bool Redirect);
    sealed record TenantDocument([property: JsonPropertyName("legal_name")] string? LegalName,
        string Status);
    sealed record TenantMemberDocument([property: JsonPropertyName("user_id")] string UserId,
        string Affiliation);
    sealed record TenantMemberListDocument(IReadOnlyList<TenantMemberDocument> Items);
    sealed record SessionDocument(string Id);
    sealed record EmailDocument(bool Verified);
}
