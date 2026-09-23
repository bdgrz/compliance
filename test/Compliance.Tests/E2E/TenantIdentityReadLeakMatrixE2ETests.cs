using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class TenantIdentityReadLeakMatrixE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldScopeTenantMemberInvitationAndSelfReadsGivenTwoTenants(bool splitHosts)
    {
        // Arrange: two unrelated owners, tenants, and invitation populations share one broker.
        var applicationName = $"compliance-identity-read-{Guid.NewGuid():N}";
        var ownerAEmail = $"owner-a-{Guid.NewGuid():N}@example.com";
        // Both theory cases share the broker's fixed platform-operator roster stream.
        const string operatorEmail = "operator-identity-read-matrix@example.com";
        var ownerBEmail = $"owner-b-{Guid.NewGuid():N}@example.com";
        var outsiderEmail = $"outsider-{Guid.NewGuid():N}@example.com";
        var invitationsA = new[]
        {
            $"a-one-{Guid.NewGuid():N}@example.com",
            $"a-two-{Guid.NewGuid():N}@example.com",
        };
        var invitationsB = new[]
        {
            $"b-one-{Guid.NewGuid():N}@example.com",
            $"b-two-{Guid.NewGuid():N}@example.com",
        };
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
            Uuid ownerAId;
            Uuid ownerBId;
            Uuid operatorId;
            Uuid tenantA;
            Uuid tenantB;
            await using (var seedFactory = E2EAppFactory.Create(broker, applicationName))
            {
                using var ownerA = CreateClient(seedFactory, splitHosts);
                using var ownerB = CreateClient(seedFactory, splitHosts);
                using var bootstrapOperator = CreateClient(seedFactory, splitHosts);
                ownerAId = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(ownerA, ownerAEmail),
                    CultureInfo.InvariantCulture);
                ownerBId = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(ownerB, ownerBEmail),
                    CultureInfo.InvariantCulture);
                operatorId = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(
                    bootstrapOperator, operatorEmail), CultureInfo.InvariantCulture);
                await VerifyEmailAsync(seedFactory, worker, ownerA, ownerAId, ownerAEmail);
                await VerifyEmailAsync(seedFactory, worker, ownerB, ownerBId, ownerBEmail);
                tenantA = await RegisterTenantAsync(ownerA, "Identity Read A");
                tenantB = await RegisterTenantAsync(ownerB, "Identity Read B");
                await WaitForActiveAsync(ownerA, tenantA);
                await WaitForActiveAsync(ownerB, tenantB);
                await WaitForInvitationAuthorityAsync(ownerA, tenantA);
                await WaitForInvitationAuthorityAsync(ownerB, tenantB);
                foreach (var email in invitationsA)
                    await InviteAsync(ownerA, tenantA, email);
                foreach (var email in invitationsB)
                    await InviteAsync(ownerB, tenantB, email);
                await WaitForInvitationsAsync(ownerA, tenantA, invitationsA);
                await WaitForInvitationsAsync(ownerB, tenantB, invitationsB);
            }

            // Act
            // The operator grant is platform metadata authority, not tenant membership.
            await using var factory = E2EAppFactory.Create(broker, applicationName)
                .WithWebHostBuilder(host => host.ConfigureTestServices(services =>
                    services.AddSingleton(new PlatformOperatorAuthority([operatorId]))));
            using var ownerAClient = CreateClient(factory, splitHosts);
            using var ownerBClient = CreateClient(factory, splitHosts);
            using var operatorClient = CreateClient(factory, splitHosts);
            using var outsider = CreateClient(factory, splitHosts);
            Assert.Equal(ownerAId, Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(
                ownerAClient, ownerAEmail), CultureInfo.InvariantCulture));
            Assert.Equal(ownerBId, Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(
                ownerBClient, ownerBEmail), CultureInfo.InvariantCulture));
            Assert.Equal(operatorId, Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(
                operatorClient, operatorEmail), CultureInfo.InvariantCulture));
            await TenantInvitationE2ETests.LoginAsync(outsider, outsiderEmail);

            // Assert
            // Current tenant metadata permits an operator; all other reads retain their own rules.
            AssertTenant(await ReadHttpAsync(ownerBClient, TenantPath(tenantB)), tenantB);
            AssertTenant(await ReadHttpAsync(ownerAClient, TenantPath(tenantA)), tenantA);
            AssertTenant(await ReadHttpAsync(operatorClient, TenantPath(tenantA)), tenantA);
            AssertTenant(await ReadHttpAsync(operatorClient, TenantPath(tenantB)), tenantB);
            await AssertDeniedHttpAsync(ownerBClient, TenantPath(tenantA), HttpStatusCode.NotFound,
                "Identity Read A");
            await AssertDeniedHttpAsync(outsider, TenantPath(tenantB), HttpStatusCode.NotFound,
                "Identity Read B");

            Assert.Equal([tenantA.ToString()], await ReadMyTenantIdsAsync(ownerAClient));
            Assert.Equal([tenantB.ToString()], await ReadMyTenantIdsAsync(ownerBClient));
            Assert.Empty(await ReadMyTenantIdsAsync(outsider));
            Assert.Empty(await ReadMyTenantIdsAsync(operatorClient));
            using (var anonymous = factory.CreateClient())
                await AssertDeniedHttpAsync(anonymous, "/api/v1/tenants/mine",
                    HttpStatusCode.Unauthorized, "Identity Read A");

            var membersA = await ReadIdsAsync(operatorClient, TenantPath(tenantA) + "/members",
                "user_id");
            var membersB = await ReadIdsAsync(operatorClient, TenantPath(tenantB) + "/members",
                "user_id");
            Assert.Equal([ownerAId.ToString()], membersA);
            Assert.Equal([ownerBId.ToString()], membersB);
            await AssertDeniedHttpAsync(ownerBClient, TenantPath(tenantB) + "/members",
                HttpStatusCode.Forbidden, ownerAEmail);
            await AssertDeniedHttpAsync(outsider, TenantPath(tenantA) + "/members",
                HttpStatusCode.Forbidden, ownerAEmail);

            Assert.Equal(invitationsA.Order(StringComparer.Ordinal),
                (await ReadInvitationEmailsAsync(ownerAClient, tenantA)).Order(StringComparer.Ordinal));
            Assert.Equal(invitationsB.Order(StringComparer.Ordinal),
                (await ReadInvitationEmailsAsync(ownerBClient, tenantB)).Order(StringComparer.Ordinal));
            await AssertDeniedHttpAsync(ownerBClient, TenantPath(tenantA) + "/member-invitations",
                HttpStatusCode.NotFound, invitationsA[0]);
            await AssertDeniedHttpAsync(ownerAClient, TenantPath(tenantB) + "/member-invitations",
                HttpStatusCode.NotFound, invitationsB[0]);
            var foreignFilter = await ReadHttpAsync(ownerBClient,
                TenantPath(tenantB) + "/member-invitations?email_address=" +
                Uri.EscapeDataString(invitationsA[0]));
            Assert.Empty(foreignFilter.GetProperty("items").EnumerateArray());
            var httpInvitationCursorA = (await ReadHttpAsync(ownerAClient,
                TenantPath(tenantA) + "/member-invitations?limit=1"))
                .GetProperty("next_cursor").GetString();
            Assert.NotNull(httpInvitationCursorA);
            await AssertForeignHttpInvitationCursorRejectedAsync(ownerBClient, tenantB,
                httpInvitationCursorA);

            var accessA = await ReadHttpAsync(ownerAClient,
                TenantPath(tenantA) + $"/members/{ownerAId}/access");
            var accessB = await ReadHttpAsync(ownerBClient,
                TenantPath(tenantB) + $"/members/{ownerBId}/access");
            AssertMemberAccess(accessA, tenantA, ownerAId);
            AssertMemberAccess(accessB, tenantB, ownerBId);
            await AssertDeniedHttpAsync(ownerBClient,
                TenantPath(tenantA) + $"/members/{ownerAId}/access",
                HttpStatusCode.NotFound, ownerAEmail);
            await AssertDeniedHttpAsync(ownerBClient,
                TenantPath(tenantB) + $"/members/{ownerAId}/access",
                HttpStatusCode.NotFound, ownerAEmail);
            await AssertDeniedHttpAsync(ownerAClient,
                TenantPath(tenantB) + $"/members/{ownerBId}/access",
                HttpStatusCode.NotFound, ownerBEmail);

            await using var ownerAMcp = await McpScenario.ConnectAsync(ownerAClient,
                new Uri(ownerAClient.BaseAddress!, "/mcp"));
            await using var operatorMcp = await McpScenario.ConnectAsync(operatorClient,
                new Uri(operatorClient.BaseAddress!, "/mcp"));
            await using var ownerBMcp = await McpScenario.ConnectAsync(ownerBClient,
                new Uri(ownerBClient.BaseAddress!, "/mcp"));
            await using var outsiderMcp = await McpScenario.ConnectAsync(outsider,
                new Uri(outsider.BaseAddress!, "/mcp"));
            AssertTenant(await ReadToolAsync(ownerBMcp, "bdgrz.tenant.get",
                TenantInput(tenantB)), tenantB);
            AssertTenant(await ReadToolAsync(ownerAMcp, "bdgrz.tenant.get",
                TenantInput(tenantA)), tenantA);
            AssertTenant(await ReadToolAsync(operatorMcp, "bdgrz.tenant.get",
                TenantInput(tenantB)), tenantB);
            AssertTenant(await ReadToolAsync(operatorMcp, "bdgrz.tenant.get",
                TenantInput(tenantA)), tenantA);
            _ = await ownerBMcp.When("bdgrz.tenant.get", TenantInput(tenantA))
                .ExpectFailure("NotFound");
            _ = await outsiderMcp.When("bdgrz.tenant.get", TenantInput(tenantB))
                .ExpectFailure("NotFound");

            Assert.Equal([tenantA.ToString()], await ReadMcpMyTenantIdsAsync(ownerAMcp));
            Assert.Equal([tenantB.ToString()], await ReadMcpMyTenantIdsAsync(ownerBMcp));
            Assert.Empty(await ReadMcpMyTenantIdsAsync(outsiderMcp));
            Assert.Empty(await ReadMcpMyTenantIdsAsync(operatorMcp));
            Assert.Equal(membersA, Ids(await ReadToolAsync(operatorMcp,
                "bdgrz.tenant-member.list", TenantInput(tenantA)), "user_id"));
            Assert.Equal(membersB, Ids(await ReadToolAsync(operatorMcp,
                "bdgrz.tenant-member.list", TenantInput(tenantB)), "user_id"));
            _ = await ownerBMcp.When("bdgrz.tenant-member.list", TenantInput(tenantB))
                .ExpectFailure("Forbidden");
            _ = await outsiderMcp.When("bdgrz.tenant-member.list", TenantInput(tenantA))
                .ExpectFailure("Forbidden");

            Assert.Equal(invitationsA.Order(StringComparer.Ordinal),
                (await ReadMcpInvitationEmailsAsync(ownerAMcp, tenantA))
                .Order(StringComparer.Ordinal));
            Assert.Equal(invitationsB.Order(StringComparer.Ordinal),
                (await ReadMcpInvitationEmailsAsync(ownerBMcp, tenantB))
                .Order(StringComparer.Ordinal));
            _ = await ownerBMcp.When("bdgrz.tenant-invitation.list", TenantInput(tenantA))
                .ExpectFailure("NotFound");
            _ = await ownerAMcp.When("bdgrz.tenant-invitation.list", TenantInput(tenantB))
                .ExpectFailure("NotFound");
            var foreignMcpFilter = TenantInput(tenantB);
            foreignMcpFilter["email_address"] = invitationsA[0];
            Assert.Empty(Ids(await ReadToolAsync(ownerBMcp, "bdgrz.tenant-invitation.list",
                foreignMcpFilter), "email_address"));
            var firstMcpInvitationInput = TenantInput(tenantA);
            firstMcpInvitationInput["limit"] = 1;
            var mcpInvitationCursorA = (await ReadToolAsync(ownerAMcp,
                "bdgrz.tenant-invitation.list", firstMcpInvitationInput))
                .GetProperty("next_cursor").GetString();
            Assert.NotNull(mcpInvitationCursorA);
            await AssertForeignMcpInvitationCursorRejectedAsync(ownerBMcp, tenantB,
                mcpInvitationCursorA);

            AssertMemberAccess(await ReadToolAsync(ownerAMcp, "bdgrz.member.access.get",
                MemberInput(tenantA, ownerAId)), tenantA, ownerAId);
            AssertMemberAccess(await ReadToolAsync(ownerBMcp, "bdgrz.member.access.get",
                MemberInput(tenantB, ownerBId)), tenantB, ownerBId);
            _ = await ownerBMcp.When("bdgrz.member.access.get",
                MemberInput(tenantA, ownerAId)).ExpectFailure("NotFound");
            _ = await ownerBMcp.When("bdgrz.member.access.get",
                MemberInput(tenantB, ownerAId)).ExpectFailure("NotFound");
            _ = await ownerAMcp.When("bdgrz.member.access.get",
                MemberInput(tenantB, ownerBId)).ExpectFailure("NotFound");
            _ = await outsiderMcp.When("bdgrz.member.access.get",
                MemberInput(tenantA, ownerAId)).ExpectFailure("NotFound");
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

    static async Task VerifyEmailAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,
        IHost? worker, HttpClient client, Uuid userId, string email)
    {
        var path = $"/api/v1/users/{userId}/email-addresses/{email}";
        await WaitUntilAsync(async () =>
        {
            using var response = await client.GetAsync(path);
            return response.StatusCode == HttpStatusCode.OK;
        }, "The email reservation was not projected.");
        if ((await ReadHttpAsync(client, path)).GetProperty("verified").GetBoolean())
            return;
        using var issued = await client.PostAsync(path + "/challenges", null);
        Assert.Equal(HttpStatusCode.NoContent, issued.StatusCode);
        var delivery = (worker?.Services ?? factory.Services)
            .GetRequiredService<MockEmailChallengeDelivery>();
        string? token = null;
        await WaitUntilAsync(() =>
        {
            var found = delivery.TryGetLatest(userId, email, out token);
            return Task.FromResult(found);
        }, "The challenge token was not delivered.");
        await WaitUntilAsync(async () =>
        {
            using var response = await client.GetAsync(path + "/challenges/status");
            if (response.StatusCode != HttpStatusCode.OK)
                return false;
            var status = await ReadJsonAsync(response);
            return status.GetProperty("delivery_status").GetString() == "delivered";
        }, "The challenge delivery outcome was not recorded.");
        using var verified = await client.PostAsJsonAsync(path + "/verifications", new { token });
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        await WaitUntilAsync(async () =>
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode != HttpStatusCode.OK)
                return false;
            return (await ReadJsonAsync(response)).GetProperty("verified").GetBoolean();
        }, "The verified address was not projected.");
    }

    static async Task<Uuid> RegisterTenantAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/tenants", new
        {
            name,
            slug = $"identity-read-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Uuid.Parse((await ReadJsonAsync(response)).GetProperty("tenant_id").GetString()!,
            CultureInfo.InvariantCulture);
    }

    static Task WaitForActiveAsync(HttpClient client, Uuid tenantId) =>
        WaitUntilAsync(async () =>
        {
            using var response = await client.GetAsync(TenantPath(tenantId));
            return response.StatusCode == HttpStatusCode.OK &&
                   (await ReadJsonAsync(response)).GetProperty("status").GetString() == "active";
        }, $"Tenant {tenantId} did not activate.");

    static Task WaitForInvitationAuthorityAsync(HttpClient client, Uuid tenantId) =>
        WaitUntilAsync(async () =>
        {
            using var response = await client.GetAsync(
                TenantPath(tenantId) + "/member-invitations");
            return response.StatusCode == HttpStatusCode.OK;
        }, $"Invitation authority for {tenantId} did not become available.");

    static async Task InviteAsync(HttpClient client, Uuid tenantId, string email)
    {
        using var response = await client.PostAsJsonAsync(
            TenantPath(tenantId) + "/member-invitations", new
            {
                email_address = email,
                built_in_role = BuiltInRbac.ComplianceParticipationRole,
            });
        Assert.True(response.StatusCode == HttpStatusCode.NoContent,
            $"Invitation returned {response.StatusCode}: " +
            await response.Content.ReadAsStringAsync());
    }

    static Task WaitForInvitationsAsync(HttpClient client, Uuid tenantId,
        IReadOnlyList<string> expected) =>
        WaitUntilAsync(async () =>
        {
            var page = await ReadHttpAsync(client, TenantPath(tenantId) + "/member-invitations");
            return expected.All(email => Ids(page, "email_address").Contains(email));
        }, $"Invitations for {tenantId} were not projected.");

    static async Task<IReadOnlyList<string>> ReadInvitationEmailsAsync(HttpClient client, Uuid tenantId)
    {
        var emails = new List<string>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var path = TenantPath(tenantId) + "/member-invitations?limit=1";
            if (cursor is not null)
                path += "&cursor=" + Uri.EscapeDataString(cursor);
            var page = await ReadHttpAsync(client, path);
            Assert.True(Ids(page, "email_address").Length <= 1);
            emails.AddRange(Ids(page, "email_address"));
            cursor = page.GetProperty("next_cursor").GetString();
            Assert.True(++pages <= 10, "The invitation cursor did not terminate.");
        } while (cursor is not null);
        Assert.Equal(2, emails.Count);
        return emails;
    }

    static async Task<IReadOnlyList<string>> ReadMyTenantIdsAsync(HttpClient client)
    {
        var tenantIds = new List<string>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var path = "/api/v1/tenants/mine?limit=1";
            if (cursor is not null)
                path += "&cursor=" + Uri.EscapeDataString(cursor);
            var page = await ReadHttpAsync(client, path);
            Assert.True(Ids(page, "tenant_id").Length <= 1);
            tenantIds.AddRange(Ids(page, "tenant_id"));
            cursor = page.GetProperty("next_cursor").GetString();
            Assert.True(++pages <= 10, "The self-list cursor did not terminate.");
        } while (cursor is not null);
        return tenantIds;
    }

    static async Task<IReadOnlyList<string>> ReadMcpMyTenantIdsAsync(McpScenario mcp)
    {
        var tenantIds = new List<string>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var input = new Dictionary<string, object?> { ["limit"] = 1 };
            if (cursor is not null)
                input["cursor"] = cursor;
            var page = await ReadToolAsync(mcp, "bdgrz.tenant-membership.list-mine", input);
            Assert.True(Ids(page, "tenant_id").Length <= 1);
            tenantIds.AddRange(Ids(page, "tenant_id"));
            cursor = page.GetProperty("next_cursor").GetString();
            Assert.True(++pages <= 10, "The MCP self-list cursor did not terminate.");
        } while (cursor is not null);
        return tenantIds;
    }

    static async Task<IReadOnlyList<string>> ReadMcpInvitationEmailsAsync(McpScenario mcp,
        Uuid tenantId)
    {
        var emails = new List<string>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var input = TenantInput(tenantId);
            input["limit"] = 1;
            if (cursor is not null)
                input["cursor"] = cursor;
            var page = await ReadToolAsync(mcp, "bdgrz.tenant-invitation.list", input);
            Assert.True(Ids(page, "email_address").Length <= 1);
            emails.AddRange(Ids(page, "email_address"));
            cursor = page.GetProperty("next_cursor").GetString();
            Assert.True(++pages <= 10, "The MCP invitation cursor did not terminate.");
        } while (cursor is not null);
        Assert.Equal(2, emails.Count);
        return emails;
    }

    static async Task AssertForeignHttpInvitationCursorRejectedAsync(HttpClient client,
        Uuid tenantId, string cursor)
    {
        using var response = await client.GetAsync(TenantPath(tenantId) +
            "/member-invitations?limit=1&cursor=" + Uri.EscapeDataString(cursor));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    static async Task AssertForeignMcpInvitationCursorRejectedAsync(McpScenario mcp,
        Uuid tenantId, string cursor)
    {
        var input = TenantInput(tenantId);
        input["limit"] = 1;
        input["cursor"] = cursor;
        _ = await mcp.When("bdgrz.tenant-invitation.list", input)
            .ExpectFailure("Validation");
    }

    static async Task<string[]> ReadIdsAsync(HttpClient client, string path, string field) =>
        Ids(await ReadHttpAsync(client, path), field);

    static async Task<JsonElement> ReadHttpAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    static async Task AssertDeniedHttpAsync(HttpClient client, string path,
        HttpStatusCode expected, string privateValue)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(expected, response.StatusCode);
        Assert.DoesNotContain(privateValue, await response.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }

    static async Task<JsonElement> ReadToolAsync(McpScenario mcp, string tool,
        Dictionary<string, object?> input)
    {
        var call = await mcp.When(tool, input).ExpectSuccess();
        return Assert.IsType<JsonElement>(call.StructuredJson).GetProperty("result");
    }

    static Dictionary<string, object?> TenantInput(Uuid tenantId) =>
        new() { ["tenant_id"] = tenantId.ToString() };

    static Dictionary<string, object?> MemberInput(Uuid tenantId, Uuid userId) =>
        new()
        {
            ["tenant_id"] = tenantId.ToString(),
            ["user_id"] = userId.ToString(),
        };

    static void AssertTenant(JsonElement tenant, Uuid tenantId) =>
        Assert.Equal(tenantId.ToString(), tenant.GetProperty("tenant_id").GetString());

    static void AssertMemberAccess(JsonElement access, Uuid tenantId, Uuid userId)
    {
        Assert.Equal(tenantId.ToString(), access.GetProperty("tenant_id").GetString());
        Assert.Equal(userId.ToString(), access.GetProperty("user_id").GetString());
        Assert.Contains(access.GetProperty("effective_permissions").EnumerateArray(),
            permission => permission.GetString() == RbacPermissions.TenantAccess);
    }

    static string[] Ids(JsonElement page, string field) =>
        page.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty(field).GetString()!).ToArray();

    static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }

    static async Task WaitUntilAsync(Func<Task<bool>> predicate, string failure)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await predicate())
                return;
            await Task.Delay(250);
        }
        Assert.Fail(failure);
    }

    static HttpClient CreateClient(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,
        bool splitHosts)
    {
        var previousMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
        try
        {
            if (splitHosts)
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", "api");
            return factory.CreateClient();
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", previousMode);
        }
    }

    static string TenantPath(Uuid tenantId) => "/api/v1/tenants/" + tenantId;
}
