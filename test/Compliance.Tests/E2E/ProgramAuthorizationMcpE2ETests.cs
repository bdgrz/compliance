using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Trait("Category", "BrokerIntegration")]
public sealed class ProgramAuthorizationMcpE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldIssueAndRevokeScopedReaderGrantGivenHttpAndMcpHosts(bool splitHosts)
    {
        // Arrange
        var applicationName = $"compliance-grant-lifecycle-{Guid.NewGuid():N}";
        var worker = splitHosts ? BuildWorker(applicationName) : null;
        if (worker is not null)
            await worker.StartAsync();

        try
        {
            await using var factory = E2EAppFactory.Create(broker, applicationName);
            var priorMode = TestHostMode.Current;
            HttpClient administrator;
            HttpClient member;
            try
            {
                TestHostMode.Set(splitHosts ? "api" : "standalone");
                administrator = factory.CreateClient();
                member = factory.CreateClient();
            }
            finally
            {
                TestHostMode.Set(priorMode);
            }

            using (administrator)
            using (member)
            {
                var administratorUserId = await TenantInvitationE2ETests.LoginAsync(administrator,
                    $"grant-admin-{Guid.NewGuid():N}@example.com");
                var memberUserId = await TenantInvitationE2ETests.LoginAsync(member,
                    $"grant-member-{Guid.NewGuid():N}@example.com");
                using var registered = await administrator.PostAsJsonAsync("/api/v1/tenants", new
                {
                    name = "Access grant lifecycle",
                    slug = $"grant-{Guid.NewGuid():N}"[..24],
                });
                Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
                using var tenantDocument = JsonDocument.Parse(
                    await registered.Content.ReadAsStringAsync());
                var tenantId = Uuid.Parse(tenantDocument.RootElement.GetProperty("tenant_id")
                    .GetString()!, CultureInfo.InvariantCulture);
                await WaitForPermissionsAsync(administrator, tenantId, administratorUserId,
                    BuiltInRbac.TenantAdministrationRole, RbacPermissions.TenantRbacManage);
                await AccessGrantE2ESupport.IssueFounderOrganizationGrantAsync(administrator,
                    tenantId);
                var programsPath = $"/api/v1/tenants/{tenantId}/programs";
                var plan = Plan("Grant lifecycle");
                using var firstCreated = await administrator.PostAsJsonAsync(programsPath,
                    new { name = "P1", plan });
                using var secondCreated = await administrator.PostAsJsonAsync(programsPath,
                    new { name = "P2", plan });
                Assert.Equal(HttpStatusCode.OK, firstCreated.StatusCode);
                Assert.Equal(HttpStatusCode.OK, secondCreated.StatusCode);
                using var firstDocument = JsonDocument.Parse(
                    await firstCreated.Content.ReadAsStringAsync());
                using var secondDocument = JsonDocument.Parse(
                    await secondCreated.Content.ReadAsStringAsync());
                var firstProgramId = firstDocument.RootElement.GetProperty("program_id").GetString()!;
                var secondProgramId = secondDocument.RootElement.GetProperty("program_id").GetString()!;
                var firstPath = $"{programsPath}/{firstProgramId}";
                var secondPath = $"{programsPath}/{secondProgramId}";
                await WaitForProgramAsync(administrator, firstPath, 1);
                await WaitForProgramAsync(administrator, secondPath, 1);

                var memberId = Uuid.Parse(memberUserId, CultureInfo.InvariantCulture);
                await using (var scope = (worker?.Services ?? factory.Services).CreateAsyncScope())
                {
                    var registeredMember = await scope.ServiceProvider.GetRequiredService<IRequestBus>()
                        .DispatchAsync(new RegisterMember(tenantId, memberId),
                            new RequestDispatchContext(RequestActor.System));
                    Assert.True(registeredMember.IsSuccess);
                }
                var deniedDeadline = DateTimeOffset.UtcNow.AddSeconds(120);
                var standingDenied = false;
                string? lastStandingResponse = null;
                while (DateTimeOffset.UtcNow < deniedDeadline)
                {
                    using var response = await member.GetAsync(firstPath);
                    if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        standingDenied = true;
                        break;
                    }
                    lastStandingResponse = $"{(int)response.StatusCode} " +
                        await response.Content.ReadAsStringAsync();
                    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                    await Task.Delay(250);
                }
                Assert.True(standingDenied,
                    $"The member did not reach active ungranted state; last GET: {lastStandingResponse}");

                var roleId = Uuid.CreateVersion4();
                var rolePath = $"/api/v1/tenants/{tenantId}/roles/{roleId}";
                using (var defined = await administrator.PostAsJsonAsync(rolePath,
                           new { name = "Reader" }))
                    Assert.Equal(HttpStatusCode.NoContent, defined.StatusCode);
                using (var assigned = await administrator.PostAsync(
                           $"{rolePath}/permissions/{RbacPermissions.TenantAccess}", null))
                    Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);
                var grantId = Uuid.CreateVersion4();
                var grantPath = $"/api/v1/tenants/{tenantId}/access-grants/{grantId}";
                var proposal = new
                {
                    principal = new { kind = "member", id = RbacIds.Member(tenantId, memberId).ToString() },
                    role_id = roleId.ToString(),
                    scope = new { kind = "program", id = firstProgramId },
                    source = new { kind = "manual", id = "http-reader-test" },
                    effective_from = DateTimeOffset.UtcNow.AddMinutes(-1),
                    effective_until = (DateTimeOffset?)null,
                };
                var issueDeadline = DateTimeOffset.UtcNow.AddSeconds(120);
                var issuedGrant = false;
                string? lastIssueResponse = null;
                while (DateTimeOffset.UtcNow < issueDeadline)
                {
                    using var issued = await administrator.PostAsJsonAsync(grantPath,
                        new { proposal });
                    if (issued.StatusCode == HttpStatusCode.NoContent)
                    {
                        issuedGrant = true;
                        break;
                    }
                    lastIssueResponse = $"{(int)issued.StatusCode} " +
                        await issued.Content.ReadAsStringAsync();
                    Assert.Equal(HttpStatusCode.NotFound, issued.StatusCode);
                    await Task.Delay(250);
                }
                Assert.True(issuedGrant,
                    $"Reader grant {grantId} could not be issued; last POST: {lastIssueResponse}");
                await WaitForGrantAsync(administrator, tenantId, grantId);

                // Act
                await WaitForProgramAsync(member, firstPath, 1);
                using (var allowed = await member.GetAsync(firstPath))
                    Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
                using (var denied = await member.GetAsync(secondPath))
                    Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
                await using var memberMcp = await McpScenario.ConnectAsync(member,
                    new Uri(member.BaseAddress!, "/mcp"));
                _ = await memberMcp.When("bdgrz.program.get", new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantId.ToString(),
                    ["program_id"] = firstProgramId,
                }).ExpectSuccess();
                _ = await memberMcp.When("bdgrz.program.get", new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantId.ToString(),
                    ["program_id"] = secondProgramId,
                }).ExpectFailure("Forbidden");
                using (var access = await administrator.GetAsync(
                           $"/api/v1/tenants/{tenantId}/members/{memberUserId}/access"))
                {
                    Assert.Equal(HttpStatusCode.OK, access.StatusCode);
                    using var document = JsonDocument.Parse(await access.Content.ReadAsStringAsync());
                    var path = Assert.Single(document.RootElement.GetProperty("grant_paths")
                        .EnumerateArray());
                    Assert.True(path.GetProperty("is_effective").GetBoolean());
                    Assert.Equal(firstProgramId, path.GetProperty("grant").GetProperty("terms")
                        .GetProperty("scope").GetProperty("id").GetString());
                    Assert.Contains(RbacPermissions.TenantAccess,
                        path.GetProperty("permissions")
                            .EnumerateArray().Select(permission => permission.GetString()));
                    Assert.Empty(document.RootElement.GetProperty("effective_permissions")
                        .EnumerateArray());
                }

                var checkpointIdentity = new CheckpointIdentity("AccessGrantsV1",
                    EventStreamPattern.ForPattern(tenantId.ToString()));
                await using var checkpointScope = factory.Services.CreateAsyncScope();
                var checkpoints = checkpointScope.ServiceProvider
                    .GetRequiredService<IProjectionCheckpointStore>();
                var beforeRevocation = await checkpoints.LoadAsync(checkpointIdentity);
                if (splitHosts)
                {
                    await worker!.StopAsync();
                    worker.Dispose();
                    worker = null;
                }
                using (var revoked = await administrator.DeleteAsync(grantPath))
                    Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
                if (splitHosts)
                {
                    var duringLag = await checkpoints.LoadAsync(checkpointIdentity);
                    Assert.Equal(beforeRevocation.Cursor, duringLag.Cursor);
                    var pending = false;
                    await foreach (var record in checkpointScope.ServiceProvider
                                       .GetRequiredService<IEventStore>()
                                       .ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString()),
                                           duringLag.Cursor, CancellationToken.None))
                        pending |= record.Event is AccessGrantRevoked revokedFact &&
                            revokedFact.GrantId == grantId;
                    Assert.True(pending);
                }
                using (var denied = await member.GetAsync(firstPath))
                    Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
                _ = await memberMcp.When("bdgrz.program.get", new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantId.ToString(),
                    ["program_id"] = firstProgramId,
                }).ExpectFailure("Forbidden");
                using (var access = await administrator.GetAsync(
                           $"/api/v1/tenants/{tenantId}/members/{memberUserId}/access"))
                {
                    Assert.Equal(HttpStatusCode.OK, access.StatusCode);
                    using var document = JsonDocument.Parse(await access.Content.ReadAsStringAsync());
                    Assert.False(Assert.Single(document.RootElement.GetProperty("grant_paths")
                        .EnumerateArray()).GetProperty("is_effective").GetBoolean());
                    Assert.DoesNotContain(RbacPermissions.TenantAccess,
                        document.RootElement.GetProperty("effective_permissions")
                            .EnumerateArray().Select(permission => permission.GetString()));
                }
                if (splitHosts)
                {
                    worker = BuildWorker(applicationName);
                    await worker.StartAsync();
                }
                await WaitForRevocationAsync(administrator, tenantId, grantId);
                using (var history = await administrator.GetAsync(
                           $"/api/v1/tenants/{tenantId}/access-grants"))
                {
                    // Assert
                    Assert.Equal(HttpStatusCode.OK, history.StatusCode);
                    using var document = JsonDocument.Parse(await history.Content.ReadAsStringAsync());
                    var grant = Assert.Single(document.RootElement.GetProperty("grants")
                        .EnumerateArray(), item =>
                        item.GetProperty("grant_id").GetString() == grantId.ToString());
                    Assert.Equal("http-reader-test", grant.GetProperty("terms").GetProperty("source")
                        .GetProperty("id").GetString());
                    var expectedActorId = RbacIds.Member(tenantId,
                        Uuid.Parse(administratorUserId, CultureInfo.InvariantCulture)).ToString();
                    Assert.Equal(expectedActorId, grant.GetProperty("terms")
                        .GetProperty("granted_by").GetProperty("id").GetString());
                    Assert.Equal(expectedActorId, grant.GetProperty("revoked_by")
                        .GetProperty("id").GetString());
                    Assert.Equal(JsonValueKind.String, grant.GetProperty("revoked_at").ValueKind);
                }
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldAllowProgramMcpFlowAndDenyManagementGivenSameTenantParticipant(
        bool splitHosts)
    {
        // Arrange
        var applicationName = $"compliance-program-auth-mcp-{Guid.NewGuid():N}";
        IHost? worker = null;
        if (splitHosts)
        {
            worker = BuildWorker(applicationName);
            await worker.StartAsync();
        }

        try
        {
            await using var factory = E2EAppFactory.Create(broker, applicationName);
            var priorMode = TestHostMode.Current;
            HttpClient administrator;
            HttpClient participant;
            try
            {
                TestHostMode.Set(splitHosts ? "api" : "standalone");
                administrator = factory.CreateClient();
                participant = factory.CreateClient();
            }
            finally
            {
                TestHostMode.Set(priorMode);
            }
            using (administrator)
            using (participant)
            {
                var administratorId = await TenantInvitationE2ETests.LoginAsync(administrator,
                    $"program-admin-{Guid.NewGuid():N}@example.com");
                var participantEmail = $"program-participant-{Guid.NewGuid():N}@example.com";
                var participantId = await TenantInvitationE2ETests.LoginAsync(participant,
                    participantEmail);
                using var tenantResponse = await administrator.PostAsJsonAsync("/api/v1/tenants",
                    new
                    {
                        name = "Program authorization MCP",
                        slug = $"program-mcp-{Guid.NewGuid():N}"[..24],
                    });
                Assert.Equal(HttpStatusCode.OK, tenantResponse.StatusCode);
                var tenant = await tenantResponse.Content.ReadFromJsonAsync<TenantDocument>();
                Assert.NotNull(tenant);
                var tenantId = Uuid.Parse(tenant.TenantId, CultureInfo.InvariantCulture);
                await WaitForPermissionsAsync(administrator, tenantId, administratorId,
                    BuiltInRbac.TenantAdministrationRole, RbacPermissions.ProgramManage);
                await AccessGrantE2ESupport.IssueFounderOrganizationGrantAsync(administrator, tenantId);

                var programsPath = $"/api/v1/tenants/{tenantId}/programs";
                var plan = Plan("Administrator");
                var createInput = new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenant.TenantId,
                    ["name"] = "MCP managed program",
                    ["plan"] = plan,
                };
                await using var administratorMcp = await McpScenario.ConnectAsync(administrator,
                    new Uri(administrator.BaseAddress!, "/mcp"));

                // Act: create, read, and list using the machine-facing contract.
                var created = await administratorMcp.When("bdgrz.program.create", createInput)
                    .ExpectSuccess();
                var registration = Assert.IsType<JsonElement>(created.StructuredJson)
                    .GetProperty("result");
                var programId = Assert.IsType<string>(registration.GetProperty("program_id")
                    .GetString());
                var programPath = $"{programsPath}/{programId}";
                await WaitForProgramAsync(administrator, programPath, 1);
                var readInput = new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenant.TenantId,
                    ["program_id"] = programId,
                    ["minimum_revision"] = 1,
                };
                var read = await administratorMcp.When("bdgrz.program.get", readInput)
                    .ExpectSuccess();
                var current = Assert.IsType<JsonElement>(read.StructuredJson).GetProperty("result");
                Assert.Equal("MCP managed program", current.GetProperty("name").GetString());
                Assert.Equal(1, current.GetProperty("revision").GetInt64());
                var listInput = new Dictionary<string, object?> { ["tenant_id"] = tenant.TenantId };
                var list = await administratorMcp.When("bdgrz.program.list", listInput)
                    .ExpectSuccess();
                var programs = Assert.IsType<JsonElement>(list.StructuredJson).GetProperty("result");
                Assert.Equal(programId, Assert.Single(programs.GetProperty("items").EnumerateArray())
                    .GetProperty("program_id").GetString());

                // Accept a same-tenant member with tenant.access but no program.manage.
                var invitePath = $"/api/v1/tenants/{tenantId}/member-invitations";
                using var invited = await administrator.PostAsJsonAsync(invitePath, new
                {
                    email_address = participantEmail,
                    built_in_role = BuiltInRbac.ComplianceParticipationRole,
                });
                Assert.Equal(HttpStatusCode.NoContent, invited.StatusCode);
                var delivery = splitHosts
                    ? worker!.Services.GetRequiredService<MockTenantInvitationDelivery>()
                    : factory.Services.GetRequiredService<MockTenantInvitationDelivery>();
                string? token = null;
                var deliveryDeadline = DateTimeOffset.UtcNow.AddSeconds(120);
                while (DateTimeOffset.UtcNow < deliveryDeadline &&
                       !delivery.TryGetLatest(tenantId, participantEmail, out token))
                    await Task.Delay(250);
                Assert.NotNull(token);
                await TenantInvitationE2ETests.VerifyEmailAsync(factory, participant,
                    participantId, participantEmail,
                    splitHosts ? worker!.Services.GetRequiredService<MockEmailChallengeDelivery>() : null);
                await WaitForInvitationDeliveryAsync(administrator, tenantId, participantEmail);
                using var accepted = await participant.PostAsJsonAsync(
                    $"/api/v1/tenants/{tenantId}/invitations/acceptance",
                    new { email_address = participantEmail, token });
                Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
                var effectivePermissions = await WaitForPermissionsAsync(administrator,
                    tenantId, participantId, BuiltInRbac.ComplianceParticipationRole,
                    RbacPermissions.TenantAccess);
                Assert.DoesNotContain(RbacPermissions.ProgramManage, effectivePermissions);
                using (var standingPermissionDenied = await participant.GetAsync(programPath))
                    Assert.Equal(HttpStatusCode.Forbidden, standingPermissionDenied.StatusCode);
                var participantMemberId = RbacIds.Member(tenantId,
                    Uuid.Parse(participantId, CultureInfo.InvariantCulture));
                var grantId = Uuid.CreateVersion4();
                var grantPath = $"/api/v1/tenants/{tenantId}/access-grants/{grantId}";
                using (var issued = await administrator.PostAsJsonAsync(grantPath, new
                {
                    proposal = new
                    {
                        principal = new { kind = "member", id = participantMemberId.ToString() },
                        role_id = BuiltInRbac.ComplianceParticipationRoleId(tenantId).ToString(),
                        scope = new { kind = "program", id = programId },
                        source = new { kind = "manual", id = "program-auth-mcp-e2e" },
                        effective_from = DateTimeOffset.UtcNow.AddMinutes(-1),
                        effective_until = (DateTimeOffset?)null,
                    },
                }))
                    Assert.Equal(HttpStatusCode.NoContent, issued.StatusCode);
                await WaitForGrantAsync(administrator, tenantId, grantId);

                using var participantRead = await participant.GetAsync(programPath);
                using var participantList = await participant.GetAsync(programsPath);
                Assert.Equal(HttpStatusCode.OK, participantRead.StatusCode);
                Assert.Equal(HttpStatusCode.OK, participantList.StatusCode);
                using (var readBody = JsonDocument.Parse(
                           await participantRead.Content.ReadAsStringAsync()))
                {
                    Assert.Equal(tenant.TenantId,
                        readBody.RootElement.GetProperty("tenant_id").GetString());
                    Assert.Equal(programId,
                        readBody.RootElement.GetProperty("program_id").GetString());
                    Assert.Equal("MCP managed program",
                        readBody.RootElement.GetProperty("name").GetString());
                }
                using (var listBody = JsonDocument.Parse(
                           await participantList.Content.ReadAsStringAsync()))
                {
                    var item = Assert.Single(listBody.RootElement.GetProperty("items")
                        .EnumerateArray());
                    Assert.Equal(programId, item.GetProperty("program_id").GetString());
                }
                using var deniedCreate = await participant.PostAsJsonAsync(programsPath,
                    new { name = "Unauthorized program", plan });
                using var deniedRevise = await participant.PutAsJsonAsync(programPath,
                    new { expected_revision = 1, name = "Unauthorized revision", plan });

                await using var participantMcp = await McpScenario.ConnectAsync(participant,
                    new Uri(participant.BaseAddress!, "/mcp"));
                var participantMcpRead = await participantMcp.When("bdgrz.program.get", readInput)
                    .ExpectSuccess();
                var participantMcpProgram = Assert.IsType<JsonElement>(
                    participantMcpRead.StructuredJson).GetProperty("result");
                Assert.Equal(programId, participantMcpProgram.GetProperty("program_id").GetString());
                Assert.Equal("MCP managed program",
                    participantMcpProgram.GetProperty("name").GetString());
                var participantMcpList = await participantMcp.When("bdgrz.program.list", listInput)
                    .ExpectSuccess();
                var participantMcpPage = Assert.IsType<JsonElement>(
                    participantMcpList.StructuredJson).GetProperty("result");
                Assert.Equal(programId, Assert.Single(participantMcpPage.GetProperty("items")
                    .EnumerateArray()).GetProperty("program_id").GetString());
                var deniedMcpCreate = participantMcp.When("bdgrz.program.create",
                    new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenant.TenantId,
                        ["name"] = "Unauthorized MCP program",
                        ["plan"] = plan,
                    });
                var reviseInput = new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenant.TenantId,
                    ["program_id"] = programId,
                    ["expected_revision"] = 1,
                    ["name"] = "MCP revised program",
                    ["plan"] = Plan("Reviser"),
                };
                var deniedMcpRevise = participantMcp.When("bdgrz.program.revise", reviseInput);

                // The participant may read the platform criteria catalog but not select it.
                using var participantEditions = await participant.GetAsync(
                    $"/api/v1/tenants/{tenantId}/criteria-editions");
                Assert.Equal(HttpStatusCode.OK, participantEditions.StatusCode);
                string editionId;
                using (var editionsBody = JsonDocument.Parse(
                           await participantEditions.Content.ReadAsStringAsync()))
                    editionId = Assert.Single(editionsBody.RootElement.EnumerateArray())
                        .GetProperty("edition_id").GetString()!;
                using var participantEntry = await participant.GetAsync(
                    $"/api/v1/tenants/{tenantId}/criteria-editions/{editionId}/entries/CC6.1");
                Assert.Equal(HttpStatusCode.OK, participantEntry.StatusCode);
                _ = await participantMcp.When("bdgrz.criteria.entries.list",
                    new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenant.TenantId,
                        ["edition_id"] = editionId,
                        ["limit"] = 5,
                    }).ExpectSuccess();
                using var deniedSelect = await participant.PutAsJsonAsync(
                    $"{programPath}/criteria-edition",
                    new { expected_revision = 1, edition_id = editionId });
                var selectInput = new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenant.TenantId,
                    ["program_id"] = programId,
                    ["expected_revision"] = 1,
                    ["edition_id"] = editionId,
                };
                var deniedMcpSelect = participantMcp.When("bdgrz.program.criteria.select",
                    selectInput);

                // Assert: every participant write is denied and none reaches the source stream.
                Assert.Equal(HttpStatusCode.Forbidden, deniedCreate.StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, deniedRevise.StatusCode);
                _ = await deniedMcpCreate.ExpectFailure("Forbidden");
                _ = await deniedMcpRevise.ExpectFailure("Forbidden");
                Assert.Equal(HttpStatusCode.Forbidden, deniedSelect.StatusCode);
                _ = await deniedMcpSelect.ExpectFailure("Forbidden");
                var sourceEvents = new List<object>();
                await foreach (var record in factory.Services.GetRequiredService<IEventStore>()
                                   .ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                                           "programs"), EventCursor.Start, CancellationToken.None))
                {
                    sourceEvents.Add(record.Event);
                }
                var createdEvent = Assert.IsType<ProgramCreated>(Assert.Single(sourceEvents));
                Assert.Equal(programId, createdEvent.ProgramId.ToString());
                using (var unchanged = JsonDocument.Parse(
                           await participant.GetStringAsync(programPath)))
                {
                    Assert.Equal(1, unchanged.RootElement.GetProperty("revision").GetInt64());
                    Assert.Equal("MCP managed program",
                        unchanged.RootElement.GetProperty("name").GetString());
                }

                // The administrator can revise through MCP; denied requests did not write.
                _ = await administratorMcp.When("bdgrz.program.revise", reviseInput)
                    .ExpectSuccess();
                await WaitForProgramAsync(administrator, programPath, 2);
                readInput["minimum_revision"] = 2;
                var revisedRead = await administratorMcp.When("bdgrz.program.get", readInput)
                    .ExpectSuccess();
                var revised = Assert.IsType<JsonElement>(revisedRead.StructuredJson)
                    .GetProperty("result");
                Assert.Equal("MCP revised program", revised.GetProperty("name").GetString());
                Assert.Equal(2, revised.GetProperty("revision").GetInt64());
                var revisedList = await administratorMcp.When("bdgrz.program.list", listInput)
                    .ExpectSuccess();
                var listed = Assert.IsType<JsonElement>(revisedList.StructuredJson)
                    .GetProperty("result");
                var onlyProgram = Assert.Single(listed.GetProperty("items").EnumerateArray());
                Assert.Equal(programId, onlyProgram.GetProperty("program_id").GetString());
                Assert.Equal(2, onlyProgram.GetProperty("revision").GetInt64());

                using var secondCreated = await administrator.PostAsJsonAsync(programsPath,
                    new { name = "Second program", plan });
                Assert.Equal(HttpStatusCode.OK, secondCreated.StatusCode);
                using var secondDocument = JsonDocument.Parse(
                    await secondCreated.Content.ReadAsStringAsync());
                var secondProgramId = secondDocument.RootElement.GetProperty("program_id").GetString()!;
                var secondProgramPath = $"{programsPath}/{secondProgramId}";
                await WaitForProgramAsync(administrator, secondProgramPath, 1);
                using (var differentProgram = await participant.GetAsync(secondProgramPath))
                    Assert.Equal(HttpStatusCode.Forbidden, differentProgram.StatusCode);
                _ = await participantMcp.When("bdgrz.program.get", new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenant.TenantId,
                    ["program_id"] = secondProgramId,
                }).ExpectFailure("Forbidden");
                using (var missingProgram = await participant.GetAsync(
                           $"{programsPath}/{Uuid.CreateVersion4()}"))
                    Assert.Equal(HttpStatusCode.NotFound, missingProgram.StatusCode);

                var checkpointIdentity = new CheckpointIdentity("AccessGrantsV1",
                    EventStreamPattern.ForPattern(tenantId.ToString()));
                await using var checkpointScope = factory.Services.CreateAsyncScope();
                var checkpoints = checkpointScope.ServiceProvider
                    .GetRequiredService<IProjectionCheckpointStore>();
                var checkpointBeforeRevoke = await checkpoints.LoadAsync(checkpointIdentity);
                if (splitHosts)
                {
                    await worker!.StopAsync();
                    worker.Dispose();
                    worker = null;
                }

                using (var revoked = await administrator.DeleteAsync(grantPath))
                    Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
                if (splitHosts)
                {
                    var checkpointDuringLag = await checkpoints.LoadAsync(checkpointIdentity);
                    Assert.Equal(checkpointBeforeRevoke.Cursor, checkpointDuringLag.Cursor);
                    var pendingRevoke = false;
                    await foreach (var record in checkpointScope.ServiceProvider
                                       .GetRequiredService<IEventStore>()
                                       .ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString()),
                                           checkpointDuringLag.Cursor, CancellationToken.None))
                        pendingRevoke |= record.Event is AccessGrantRevoked revokedFact &&
                            revokedFact.GrantId == grantId;
                    Assert.True(pendingRevoke);
                }
                using (var revokedRead = await participant.GetAsync(programPath))
                    Assert.Equal(HttpStatusCode.Forbidden, revokedRead.StatusCode);
                _ = await participantMcp.When("bdgrz.program.get", readInput)
                    .ExpectFailure("Forbidden");
                using (var accessResponse = await administrator.GetAsync(
                           $"/api/v1/tenants/{tenantId}/members/{participantId}/access"))
                {
                    Assert.Equal(HttpStatusCode.OK, accessResponse.StatusCode);
                    using var accessDocument = JsonDocument.Parse(
                        await accessResponse.Content.ReadAsStringAsync());
                    var path = Assert.Single(accessDocument.RootElement.GetProperty("grant_paths")
                        .EnumerateArray());
                    Assert.False(path.GetProperty("is_effective").GetBoolean());
                }

                if (splitHosts)
                {
                    worker = BuildWorker(applicationName);
                    await worker.StartAsync();
                }
                await WaitForRevocationAsync(administrator, tenantId, grantId);
                var mcpGrantId = Uuid.CreateVersion4();
                _ = await administratorMcp.When("bdgrz.access-grant.issue",
                    new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenant.TenantId,
                        ["grant_id"] = mcpGrantId.ToString(),
                        ["proposal"] = new Dictionary<string, object?>
                        {
                            ["principal"] = new Dictionary<string, object?>
                            {
                                ["kind"] = "member",
                                ["id"] = participantMemberId.ToString(),
                            },
                            ["role_id"] = BuiltInRbac.ComplianceParticipationRoleId(tenantId).ToString(),
                            ["scope"] = new Dictionary<string, object?>
                            {
                                ["kind"] = "program",
                                ["id"] = programId,
                            },
                            ["source"] = new Dictionary<string, object?>
                            {
                                ["kind"] = "manual",
                                ["id"] = "program-auth-mcp-e2e",
                            },
                            ["effective_from"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"),
                        },
                    }).ExpectSuccess();
                await WaitForGrantAsync(administrator, tenantId, mcpGrantId);
                using (var restoredRead = await participant.GetAsync(programPath))
                    Assert.Equal(HttpStatusCode.OK, restoredRead.StatusCode);
                _ = await administratorMcp.When("bdgrz.access-grant.revoke",
                    new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenant.TenantId,
                        ["grant_id"] = mcpGrantId.ToString(),
                    }).ExpectSuccess();
                using (var deniedAgain = await participant.GetAsync(programPath))
                    Assert.Equal(HttpStatusCode.Forbidden, deniedAgain.StatusCode);
                await WaitForRevocationAsync(administrator, tenantId, mcpGrantId);
                var grantList = await administratorMcp.When("bdgrz.access-grant.list",
                    new Dictionary<string, object?> { ["tenant_id"] = tenant.TenantId })
                    .ExpectSuccess();
                var grantHistory = Assert.IsType<JsonElement>(grantList.StructuredJson)
                    .GetProperty("result").GetProperty("grants");
                Assert.Contains(grantHistory.EnumerateArray(), item =>
                    item.GetProperty("grant_id").GetString() == grantId.ToString());
                Assert.Contains(grantHistory.EnumerateArray(), item =>
                    item.GetProperty("grant_id").GetString() == mcpGrantId.ToString());
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

    static async Task<IReadOnlyList<string>> WaitForPermissionsAsync(HttpClient administrator,
        Uuid tenantId, string userId, string role, string requiredPermission)
    {
        var path = $"/api/v1/tenants/{tenantId}/members/{userId}/access" +
                   $"?expected_built_in_role={role}";
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        string? lastObservation = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await administrator.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var access = await response.Content.ReadFromJsonAsync<MemberAccessDocument>();
                if (access?.EffectivePermissions.Contains(requiredPermission) == true)
                    return access.EffectivePermissions;
                lastObservation = $"200 OK; effective permissions: " +
                    string.Join(", ", access?.EffectivePermissions ?? []);
            }
            else
                lastObservation = $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}";
            await Task.Delay(250);
        }
        throw new TimeoutException($"Member {userId} in tenant {tenantId} did not acquire " +
            $"{requiredPermission}; last access response: {lastObservation}");
    }

    static async Task WaitForProgramAsync(HttpClient client, string path, long revision)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        string? lastObservation = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync($"{path}?minimum_revision={revision}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var program = await response.Content.ReadFromJsonAsync<ProgramDocument>();
                if (program?.Revision == revision)
                    return;
                lastObservation = $"200 OK; revision {program?.Revision}";
            }
            else
                lastObservation = $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}";
            await Task.Delay(250);
        }
        throw new TimeoutException($"Program {path} did not reach revision {revision}; " +
            $"last GET: {lastObservation}");
    }

    static async Task WaitForInvitationDeliveryAsync(HttpClient administrator, Uuid tenantId,
        string email)
    {
        var path = $"/api/v1/tenants/{tenantId}/member-invitations?email_address=" +
                   Uri.EscapeDataString(email);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await administrator.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using var body = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync());
                if (body.RootElement.GetProperty("items").EnumerateArray().Any(item =>
                        item.GetProperty("delivery_status").GetString() == "delivered"))
                    return;
            }
            await Task.Delay(250);
        }
        throw new TimeoutException("The invitation delivery outcome did not project.");
    }

    static async Task WaitForGrantAsync(HttpClient administrator, Uuid tenantId, Uuid grantId)
    {
        var path = $"/api/v1/tenants/{tenantId}/access-grants";
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        string? lastObservation = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await administrator.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var grants = document.RootElement.GetProperty("grants").EnumerateArray();
                if (grants.Any(grant =>
                        grant.GetProperty("grant_id").GetString() == grantId.ToString()))
                    return;
                lastObservation = $"200 OK; {grants.Count()} grant(s) projected";
            }
            else
                lastObservation = $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}";
            await Task.Delay(250);
        }
        throw new TimeoutException($"Access grant {grantId} in tenant {tenantId} did not " +
            $"project; last GET: {lastObservation}");
    }

    static async Task WaitForRevocationAsync(HttpClient administrator, Uuid tenantId, Uuid grantId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        string? lastObservation = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await administrator.GetAsync(
                $"/api/v1/tenants/{tenantId}/access-grants");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var grant = document.RootElement.GetProperty("grants").EnumerateArray()
                    .FirstOrDefault(item => item.GetProperty("grant_id").GetString() ==
                        grantId.ToString());
                if (grant.ValueKind != JsonValueKind.Undefined &&
                    grant.GetProperty("revoked_at").ValueKind == JsonValueKind.String)
                    return;
                lastObservation = grant.ValueKind == JsonValueKind.Undefined
                    ? "200 OK; grant absent"
                    : $"200 OK; revoked_at is {grant.GetProperty("revoked_at").ValueKind}";
            }
            else
                lastObservation = $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}";
            await Task.Delay(250);
        }
        throw new TimeoutException($"Access grant {grantId} in tenant {tenantId} did not " +
            $"project its revocation; last GET: {lastObservation}");
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

    static object Plan(string advisor) => new
    {
        target_readiness_date = "2027-01-31",
        target_type_i_as_of_date = "2027-03-31",
        target_type_ii_start_date = "2027-04-01",
        target_type_ii_end_date = "2028-03-31",
        readiness_advisor = advisor,
        audit_firm = (string?)null,
    };

    sealed record TenantDocument([property: JsonPropertyName("tenant_id")] string TenantId);
    sealed record MemberAccessDocument([property: JsonPropertyName("effective_permissions")]
        IReadOnlyList<string> EffectivePermissions);
    sealed record ProgramDocument(long Revision);
}
