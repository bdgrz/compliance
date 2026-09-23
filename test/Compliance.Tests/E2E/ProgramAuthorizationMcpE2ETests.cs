using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ProgramAuthorizationMcpE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
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
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                EnvironmentName = "Development",
            });
            builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
            builder.Configuration["Fitz:ApplicationName"] = applicationName;
            builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
            builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true)
                .AddWorkers();
            worker = builder.Build();
            await worker.StartAsync();
        }

        try
        {
            await using var factory = E2EAppFactory.Create(broker, applicationName);
            var priorMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
            HttpClient administrator;
            HttpClient participant;
            try
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE",
                    splitHosts ? "api" : "standalone");
                administrator = factory.CreateClient();
                participant = factory.CreateClient();
            }
            finally
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", priorMode);
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
                var deliveryDeadline = DateTimeOffset.UtcNow.AddSeconds(45);
                while (DateTimeOffset.UtcNow < deliveryDeadline &&
                       !delivery.TryGetLatest(tenantId, participantEmail, out token))
                    await Task.Delay(250);
                Assert.NotNull(token);
                await TenantInvitationE2ETests.VerifyEmailAsync(factory, participant,
                    participantId, participantEmail,
                    splitHosts ? worker!.Services.GetRequiredService<MockEmailChallengeDelivery>() : null);
                using var accepted = await participant.PostAsJsonAsync(
                    $"/api/v1/tenants/{tenantId}/invitations/acceptance",
                    new { email_address = participantEmail, token });
                Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
                var effectivePermissions = await WaitForPermissionsAsync(administrator,
                    tenantId, participantId, BuiltInRbac.ComplianceParticipationRole,
                    RbacPermissions.TenantAccess);
                Assert.DoesNotContain(RbacPermissions.ProgramManage, effectivePermissions);

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
                Assert.Equal(HttpStatusCode.Forbidden, deniedCreate.StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, deniedRevise.StatusCode);

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
                _ = await participantMcp.When("bdgrz.program.create", new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenant.TenantId,
                    ["name"] = "Unauthorized MCP program",
                    ["plan"] = plan,
                }).ExpectFailure("Forbidden");
                var reviseInput = new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenant.TenantId,
                    ["program_id"] = programId,
                    ["expected_revision"] = 1,
                    ["name"] = "MCP revised program",
                    ["plan"] = Plan("Reviser"),
                };
                _ = await participantMcp.When("bdgrz.program.revise", reviseInput)
                    .ExpectFailure("Forbidden");

                var createdEvents = new List<ProgramCreated>();
                await foreach (var record in factory.Services.GetRequiredService<IEventStore>()
                                   .ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                                           "programs"), EventCursor.Start, CancellationToken.None))
                {
                    if (record.Event is ProgramCreated createdEvent)
                        createdEvents.Add(createdEvent);
                }
                Assert.Equal(programId, Assert.Single(createdEvents).ProgramId.ToString());

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
                // Assert
                var onlyProgram = Assert.Single(listed.GetProperty("items").EnumerateArray());
                Assert.Equal(programId, onlyProgram.GetProperty("program_id").GetString());
                Assert.Equal(2, onlyProgram.GetProperty("revision").GetInt64());
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
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await administrator.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var access = await response.Content.ReadFromJsonAsync<MemberAccessDocument>();
                if (access?.EffectivePermissions.Contains(requiredPermission) == true)
                    return access.EffectivePermissions;
            }
            await Task.Delay(250);
        }
        throw new TimeoutException($"The member did not acquire {requiredPermission}.");
    }

    static async Task WaitForProgramAsync(HttpClient client, string path, long revision)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync($"{path}?minimum_revision={revision}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var program = await response.Content.ReadFromJsonAsync<ProgramDocument>();
                if (program?.Revision == revision)
                    return;
            }
            await Task.Delay(250);
        }
        throw new TimeoutException($"The program projection did not reach revision {revision}.");
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
