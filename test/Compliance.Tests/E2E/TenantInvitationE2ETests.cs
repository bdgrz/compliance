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
        await WaitForMemberAccessReadyAsync(factory, administratorClient, tenantId,
            administratorId, BuiltInRbac.AdministratorsTeamId(tenantId),
            RbacPermissions.TenantRbacManage,
            $"/api/v1/tenants/{tenantId}/members/{administratorId}");

        var memberEmail = $"member-{Guid.NewGuid():N}@example.com";
        using var invitedMember = await administratorClient.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/member-invitations",
            new { email_address = memberEmail, built_in_role = BuiltInRbac.ComplianceParticipationRole });
        Assert.Equal(HttpStatusCode.NoContent, invitedMember.StatusCode);
        var memberToken = await WaitForInvitationDeliveryReadyAsync(factory, tenantId, memberEmail);

        using var memberClient = factory.CreateClient();
        var memberUserId = await LoginAsync(memberClient, memberEmail);
        await VerifyEmailAsync(factory, memberClient, memberUserId, memberEmail);
        using var acceptedMember = await memberClient.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/invitations/acceptance",
            new { email_address = memberEmail, token = memberToken });
        Assert.Equal(HttpStatusCode.NoContent, acceptedMember.StatusCode);

        var standardUsersTeamId = BuiltInRbac.StandardUsersTeamId(tenantId);
        var memberTeamPath = $"/api/v1/tenants/{tenantId}/teams/{standardUsersTeamId}";
        await WaitForMemberAccessReadyAsync(factory, memberClient, tenantId,
            memberUserId, standardUsersTeamId, RbacPermissions.TenantAccess, memberTeamPath);
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
        var openAssignments = await MemberResponsibilityReadiness.WaitForAsync(
            factory.Services, administratorClient, tenantId, boundaryId, boundaryVersionId,
            RbacIds.Member(tenantId, Uuid.Parse(memberUserId, CultureInfo.InvariantCulture)),
            orphanedWorkPath);
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
        var membership = await WaitForMemberLifecycleReadyAsync(factory, administratorClient,
            memberClient, tenantId, memberUserId, memberTeamPath, true,
            "Access review is overdue.");
        Assert.Equal("Access review is overdue.", membership.SuspensionReason);
        Assert.Equal(RbacIds.Member(tenantId, Uuid.Parse(administratorId, CultureInfo.InvariantCulture)),
            membership.SuspendedByMemberId);
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
        var reinstatedMembership = await WaitForMemberLifecycleReadyAsync(factory,
            administratorClient, memberClient, tenantId, memberUserId, memberTeamPath, false);
        _ = await memberMcp.When("bdgrz.rbac.team.get", teamArguments).ExpectSuccess();
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
        _ = await WaitForMemberLifecycleReadyAsync(factory, administratorClient, memberClient,
            tenantId, memberUserId, memberTeamPath, true, "MCP access review is overdue.");
        _ = await memberMcp.When("bdgrz.rbac.team.get", teamArguments).ExpectFailure();
        _ = await administratorMcp.When("bdgrz.tenant.member.get", memberArguments).ExpectSuccess();
        _ = await administratorMcp.When("bdgrz.tenant.member.reinstate", memberArguments).ExpectSuccess();
        _ = await WaitForMemberLifecycleReadyAsync(factory, administratorClient, memberClient,
            tenantId, memberUserId, memberTeamPath, false);
        _ = await memberMcp.When("bdgrz.rbac.team.get", teamArguments).ExpectSuccess();

        // The current member view shows the latest transition; the event stream retains
        // both attributed cycles for audit and replay.
        var lifecycleEvents = new List<DomainEvent>();
        using (var scope = factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IEventStore>();
            await foreach (var record in store.ReadAsync(new Member(tenantId,
                               Uuid.Parse(memberUserId, CultureInfo.InvariantCulture)).Stream, 0,
                               CancellationToken.None))
            {
                if (record.Event is MemberSuspended or MemberReinstated)
                    lifecycleEvents.Add(record.Event);
            }
        }
        Assert.Collection(lifecycleEvents,
            first =>
            {
                var transition = Assert.IsType<MemberSuspended>(first);
                Assert.Equal("Access review is overdue.", transition.Reason);
                Assert.Equal(RbacIds.Member(tenantId,
                    Uuid.Parse(administratorId, CultureInfo.InvariantCulture)),
                    transition.SuspendedByMemberId);
            },
            second => Assert.IsType<MemberReinstated>(second),
            third =>
            {
                var transition = Assert.IsType<MemberSuspended>(third);
                Assert.Equal("MCP access review is overdue.", transition.Reason);
            },
            fourth => Assert.IsType<MemberReinstated>(fourth));
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

    static async Task<string> WaitForInvitationDeliveryReadyAsync(
        WebApplicationFactory<Program> factory, Uuid tenantId, string emailAddress)
    {
        var checkpoint = new CheckpointIdentity("TenantInvitationDirectory",
            EventStreamPattern.ForPattern(tenantId.ToString(), "tenant-invitations"));
        var delivery = factory.Services.GetRequiredService<MockTenantInvitationDelivery>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        var sourceEventRecorded = false;
        var directoryWorkerAdvanced = false;
        var deliveryStatus = "unknown";
        string? token = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var services = scope.ServiceProvider;
                var invitation = await services.GetRequiredService<IAggregateReader>()
                    .HydrateAsync(new TenantInvitation(tenantId, emailAddress));
                sourceEventRecorded = invitation.CurrentDeliveryAttemptId is not null;
                deliveryStatus = invitation.DeliveryStatus;
                directoryWorkerAdvanced = await services.GetRequiredService<IProjectionCheckpointStore>()
                    .LoadAsync(checkpoint) != ProjectionCheckpoint.Start;
            }
            if (sourceEventRecorded && directoryWorkerAdvanced && deliveryStatus == "delivered" &&
                delivery.TryGetLatest(tenantId, emailAddress, out token) && token is not null)
                return token;
            await Task.Delay(250);
        }
        throw new TimeoutException("Member invitation delivery did not become ready. " +
            $"sourceEventRecorded={sourceEventRecorded}; " +
            $"directoryWorkerAdvanced={directoryWorkerAdvanced}; " +
            $"deliveryStatus={deliveryStatus}; tokenPresent={token is not null}.");
    }

    static async Task<MemberLifecycleDocument> WaitForMemberLifecycleReadyAsync(
        WebApplicationFactory<Program> factory, HttpClient administratorClient,
        HttpClient memberClient, Uuid tenantId, string memberUserId, string businessRoute,
        bool suspended, string? suspensionReason = null)
    {
        var userId = Uuid.Parse(memberUserId, CultureInfo.InvariantCulture);
        var memberId = RbacIds.Member(tenantId, userId);
        var membershipCheckpoint = new CheckpointIdentity("TenantMembership",
            EventStreamPattern.ForPattern(tenantId.ToString(), "rbac-members"));
        var permissionCheckpoint = new CheckpointIdentity("PermissionProjection",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        var sourceSuspended = !suspended;
        var projectedSuspended = !suspended;
        var membershipWorkerAdvanced = false;
        var permissionWorkerAdvanced = false;
        var permissionAllowed = suspended;
        var adminStatus = HttpStatusCode.Forbidden;
        var memberStatus = HttpStatusCode.Forbidden;
        MemberLifecycleDocument? view = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var services = scope.ServiceProvider;
                var source = await services.GetRequiredService<IAggregateReader>()
                    .HydrateAsync(new Member(tenantId, userId));
                sourceSuspended = source.IsRegistered && source.IsSuspended;
                var membership = await services.GetRequiredService<ITenantMembershipDirectoryReader>()
                    .GetAsync(tenantId.ToString(), userId);
                projectedSuspended = membership?.IsSuspended ?? !suspended;
                var checkpoints = services.GetRequiredService<IProjectionCheckpointStore>();
                membershipWorkerAdvanced = await checkpoints.LoadAsync(membershipCheckpoint) !=
                    ProjectionCheckpoint.Start;
                permissionWorkerAdvanced = await checkpoints.LoadAsync(permissionCheckpoint) !=
                    ProjectionCheckpoint.Start;
                permissionAllowed = await services.GetRequiredService<IPermissionAuthorizer>()
                    .IsAllowedAsync(tenantId, userId, memberId, RbacPermissions.TenantAccess);
            }
            using (var response = await administratorClient.GetAsync(
                       $"/api/v1/tenants/{tenantId}/members/{memberUserId}"))
            {
                adminStatus = response.StatusCode;
                if (adminStatus == HttpStatusCode.OK)
                    view = await response.Content.ReadFromJsonAsync<MemberLifecycleDocument>();
            }
            using (var response = await memberClient.GetAsync(businessRoute))
                memberStatus = response.StatusCode;
            if (sourceSuspended == suspended && projectedSuspended == suspended &&
                membershipWorkerAdvanced && permissionWorkerAdvanced &&
                permissionAllowed == !suspended && adminStatus == HttpStatusCode.OK &&
                memberStatus == (suspended ? HttpStatusCode.NotFound : HttpStatusCode.OK) &&
                view?.IsSuspended == suspended &&
                (suspended ? view.SuspensionReason == suspensionReason : view.ReinstatedAt is not null))
                return view;
            await Task.Delay(250);
        }
        throw new TimeoutException($"Member lifecycle did not project. expectedSuspended={suspended}; " +
            $"sourceSuspended={sourceSuspended}; projectedSuspended={projectedSuspended}; " +
            $"membershipWorkerAdvanced={membershipWorkerAdvanced}; " +
            $"permissionWorkerAdvanced={permissionWorkerAdvanced}; " +
            $"permissionAllowed={permissionAllowed}; adminHttpStatus={adminStatus}; " +
            $"memberHttpStatus={memberStatus}; viewSuspended={view?.IsSuspended}; " +
            $"viewSuspensionReason={view?.SuspensionReason}.");
    }

    static async Task WaitForMemberAccessReadyAsync(WebApplicationFactory<Program> factory,
        HttpClient client, Uuid tenantId, string userId, Uuid teamId, string permission,
        string businessRoute)
    {
        var parsedUserId = Uuid.Parse(userId, CultureInfo.InvariantCulture);
        var memberId = RbacIds.Member(tenantId, parsedUserId);
        var membershipCheckpoint = new CheckpointIdentity("TenantMembership",
            EventStreamPattern.ForPattern(tenantId.ToString(), "rbac-members"));
        var permissionCheckpoint = new CheckpointIdentity("PermissionProjection",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        var tenantActive = false;
        var memberRegistered = false;
        var teamAssigned = false;
        var membershipProjected = false;
        var membershipWorkerAdvanced = false;
        var permissionWorkerAdvanced = false;
        var permissionAllowed = false;
        var routeStatus = HttpStatusCode.Forbidden;
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var services = scope.ServiceProvider;
                var aggregates = services.GetRequiredService<IAggregateReader>();
                tenantActive = (await aggregates.HydrateAsync(new Tenant(tenantId))).IsActive;
                memberRegistered = (await aggregates.HydrateAsync(new Member(tenantId, parsedUserId)))
                    .IsRegistered;
                teamAssigned = (await aggregates.HydrateAsync(new TeamMember(tenantId, teamId,
                    memberId))).IsAssigned;
                var membership = await services.GetRequiredService<ITenantMembershipDirectoryReader>()
                    .GetAsync(tenantId.ToString(), parsedUserId);
                membershipProjected = membership is { IsSuspended: false };
                var checkpoints = services.GetRequiredService<IProjectionCheckpointStore>();
                membershipWorkerAdvanced = await checkpoints.LoadAsync(membershipCheckpoint) !=
                    ProjectionCheckpoint.Start;
                permissionWorkerAdvanced = await checkpoints.LoadAsync(permissionCheckpoint) !=
                    ProjectionCheckpoint.Start;
                permissionAllowed = await services.GetRequiredService<IPermissionAuthorizer>()
                    .IsAllowedAsync(tenantId, parsedUserId, memberId, permission);
            }
            using var response = await client.GetAsync(businessRoute);
            routeStatus = response.StatusCode;
            if (tenantActive && memberRegistered && teamAssigned && membershipProjected &&
                membershipWorkerAdvanced && permissionWorkerAdvanced && permissionAllowed &&
                routeStatus == HttpStatusCode.OK)
                return;
            await Task.Delay(250);
        }

        Assert.Fail($"Member business access did not become ready. tenantActive={tenantActive}; " +
            $"memberRegistered={memberRegistered}; teamAssigned={teamAssigned}; " +
            $"membershipProjected={membershipProjected}; " +
            $"membershipWorkerAdvanced={membershipWorkerAdvanced}; " +
            $"permissionWorkerAdvanced={permissionWorkerAdvanced}; " +
            $"permissionAllowed={permissionAllowed}; lastHttpStatus={routeStatus}; " +
            $"permission={permission}.");
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
