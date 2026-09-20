using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

/// <summary>
///     Exercises the RBAC domain's (Team + Role) MCP surface through a real Streamable HTTP client
///     — this is what no test could verify before Portia 0.4's Mcp.Testing package: that the
///     declared tools are actually reachable, and that a call genuinely dispatches through the
///     same authorization pipeline as direct HTTP, not just that host startup didn't throw.
///     <see cref="McpToolListExpectations.ExpectExactly" /> is exact-match only, so this list must
///     be updated whenever a Program.cs <c>AddMcpTool</c> registration changes — that coupling is
///     deliberate: it forces a visible review of the registered surface instead of registration
///     drift going unnoticed.
/// </summary>
public sealed class RbacMcpScenarioTests
{
    [Fact]
    public async Task ShouldListToolsAndDenyCallGivenUnprivilegedMcpActor()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        using var login = await client.PostAsJsonAsync(
            "/api/v1/developer-user-sessions",
            new RegistrationDocument("mcp-caller@example.com"),
            CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        await using var scenario = await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp"));

        _ = await scenario.ListTools().ExpectExactly(
            "bdgrz.boundary.create",
            "bdgrz.boundary.draft.revise",
            "bdgrz.boundary.draft.discard",
            "bdgrz.boundary.successor.propose",
            "bdgrz.boundary.get",
            "bdgrz.boundary.program.list",
            "bdgrz.boundary.version.get",
            "bdgrz.boundary.version.effective.get",
            "bdgrz.boundary.versions.list",
            "bdgrz.boundary.decision.get",
            "bdgrz.boundary.decisions.list",
            "bdgrz.boundary.impact.preview",
            "bdgrz.client-service.create",
            "bdgrz.client-service.revise",
            "bdgrz.client-service.retire",
            "bdgrz.client-service.get",
            "bdgrz.client-service.list",
            "bdgrz.client-service.program.list",
            "bdgrz.client-service.revisions.list",
            "bdgrz.client-service.revision.get",
            "bdgrz.program.create",
            "bdgrz.program.revise",
            "bdgrz.program.get",
            "bdgrz.program.list",
            "bdgrz.program.revisions.list",
            "bdgrz.program.revision.get",
            "bdgrz.program.setup-work.get",
            "bdgrz.member.access.get",
            "bdgrz.rbac.team.define",
            "bdgrz.rbac.team.delete",
            "bdgrz.rbac.team.get",
            "bdgrz.rbac.team.list",
            "bdgrz.rbac.team-member.assign",
            "bdgrz.rbac.team-member.remove",
            "bdgrz.rbac.team-member.list",
            "bdgrz.rbac.role.define",
            "bdgrz.rbac.role.delete",
            "bdgrz.rbac.role.get",
            "bdgrz.rbac.role.list",
            "bdgrz.rbac.role-permission.assign",
            "bdgrz.rbac.role-permission.remove",
            "bdgrz.rbac.role-permission.list",
            "bdgrz.rbac.team-role.assign",
            "bdgrz.rbac.team-role.remove",
            "bdgrz.rbac.role-team.list",
            "bdgrz.tenant-membership.list-mine",
            "bdgrz.tenant.register",
            "bdgrz.tenant.suspend",
            "bdgrz.tenant.reactivate",
            "bdgrz.tenant-member.invite",
            "bdgrz.organization-member.invite",
            "bdgrz.tenant-invitation.list",
            "bdgrz.tenant.get",
            "bdgrz.tenant-member.list",
            "bdgrz.tenant.change-slug",
            "bdgrz.tenant-slug.resolve-mine");

        var registration = new Dictionary<string, object?>
        {
            ["name"] = "MCP organization",
            ["slug"] = $"mcp-tenant-{Guid.NewGuid():N}"[..24],
        };
        _ = await scenario.When("bdgrz.tenant.register", registration).ExpectSuccess();

        // No permission grant exists for this actor, so a call must fail the same way a direct
        // HTTP request would: TenantAccessAuthorizer denies before the handler ever runs.
        _ = await scenario.When(
                "bdgrz.rbac.team.list",
                new Dictionary<string, object?> { ["tenant_id"] = Uuid.CreateVersion4().ToString() })
            .ExpectFailure();
    }

    static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
            builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
            builder.UseSetting("Fitz:ApplicationName", "compliance-mcp-tests");
            builder.ConfigureServices(services =>
            {
                var brokerHostedServices = services
                    .Where(descriptor =>
                        descriptor.ServiceType == typeof(IHostedService) &&
                        descriptor.ImplementationType?.Name != "ComplianceReadinessLifecycle")
                    .ToArray();
                foreach (var descriptor in brokerHostedServices)
                {
                    services.Remove(descriptor);
                }

                services.RemoveAll<IEventStore>();
                services.AddSingleton<IEventStore, InMemoryEventStore>();
                services.RemoveAll<IPermissionAuthorizer>();
                services.AddSingleton<IPermissionAuthorizer, DenyAllPermissionAuthorizer>();
            });
        });

    sealed record RegistrationDocument(
        [property: JsonPropertyName("email_address")] string EmailAddress);

    sealed class DenyAllPermissionAuthorizer : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(
            Uuid tenantId,
            Uuid memberId,
            string permission,
            CancellationToken ct = default) => ValueTask.FromResult(false);
    }
}
