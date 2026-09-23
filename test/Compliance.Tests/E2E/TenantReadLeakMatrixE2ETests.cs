using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     EN-01 current-surface leak matrix: two real tenants with one member of both, plus an
///     outsider, exercise tenant-owned pages through the API and MCP in both host modes.
/// </summary>
[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class TenantReadLeakMatrixE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    static readonly string[] ImportReadSuffixes = ["", "/rows", "/preview"];
    static readonly string[] ImportReadTools =
    [
        "bdgrz.application_import.get",
        "bdgrz.application_import.rows.list",
        "bdgrz.application_import.preview",
    ];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ShouldScopeRbacListsSearchAndCursorsGivenTwoTenants(bool splitHosts) =>
        RunWithHostsAsync(splitHosts, async (owner, outsider) =>
        {
            // Arrange
            var userId = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(owner,
                "read-matrix-owner-" + Guid.NewGuid().ToString("N") + "@example.com"),
                CultureInfo.InvariantCulture);
            await TenantInvitationE2ETests.LoginAsync(outsider,
                "read-matrix-outsider-" + Guid.NewGuid().ToString("N") + "@example.com");
            var tenantA = await CreateTenantAsync(owner, "Read matrix A");
            var tenantB = await CreateTenantAsync(owner, "Read matrix B");
            var scopeA = await SeedRbacAsync(owner, tenantA, userId, "Matrix-A");
            var scopeB = await SeedRbacAsync(owner, tenantB, userId, "Matrix-B");

            // Act and assert: names, cursors, and search results stay in their own realm.
            foreach (var scope in new[] { scopeA, scopeB })
            {
                var teamsPath = TenantPath(scope.TenantId) + "/teams";
                var rolesPath = TenantPath(scope.TenantId) + "/roles";
                var teamIds = await ReadTwoHttpPagesAsync(owner,
                    teamsPath + "?search=Matrix&limit=1", "team_id");
                var roleIds = await ReadTwoHttpPagesAsync(owner,
                    rolesPath + "?search=Matrix&limit=1", "role_id");
                AssertIds(scope.TeamIds, teamIds);
                AssertIds(scope.RoleIds, roleIds);
                Assert.Empty(Ids(await ReadHttpPageAsync(owner,
                    teamsPath + "?search=" + (scope == scopeA ? "Matrix-B" : "Matrix-A")), "team_id"));
                Assert.Empty(Ids(await ReadHttpPageAsync(owner,
                    rolesPath + "?search=" + (scope == scopeA ? "Matrix-B" : "Matrix-A")), "role_id"));

                var membersPath = teamsPath + "/" + scope.AdministratorsTeamId + "/members";
                var permissionsPath = rolesPath + "/" + scope.AdministrationRoleId + "/permissions";
                var roleTeamsPath = rolesPath + "/" + scope.AdministrationRoleId + "/teams";
                var members = await WaitForPageAsync(owner, membersPath,
                    page => Ids(page, "member_id").Contains(scope.MemberId));
                Assert.Contains(scope.MemberId, Ids(members, "member_id"));
                Assert.All(Ids(members, "team_id"),
                    teamId => Assert.Equal(scope.AdministratorsTeamId, teamId));
                Assert.Contains(scope.MemberId, Ids(await ReadHttpPageAsync(owner,
                    membersPath + "?search=" + scope.MemberId[..8]), "member_id"));

                var permissions = await WaitForPageAsync(owner, permissionsPath,
                    page => Ids(page, "permission").Length >= 2);
                Assert.All(Ids(permissions, "role_id"),
                    roleId => Assert.Equal(scope.AdministrationRoleId, roleId));
                var permissionFirst = await ReadHttpPageAsync(owner,
                    permissionsPath + "?search=manage&limit=1");
                var permissionCursor = permissionFirst.GetProperty("next_cursor").GetString();
                Assert.NotNull(permissionCursor);
                var permissionSecond = await ReadHttpPageAsync(owner,
                    permissionsPath + "?search=manage&limit=1&cursor=" +
                    Uri.EscapeDataString(permissionCursor));
                Assert.All(Ids(permissionSecond, "role_id"),
                    roleId => Assert.Equal(scope.AdministrationRoleId, roleId));

                var roleTeams = await WaitForPageAsync(owner, roleTeamsPath,
                    page => Ids(page, "team_id").Contains(scope.AdministratorsTeamId));
                Assert.Contains(scope.AdministratorsTeamId, Ids(roleTeams, "team_id"));
                Assert.All(Ids(roleTeams, "role_id"),
                    roleId => Assert.Equal(scope.AdministrationRoleId, roleId));
                Assert.Contains(scope.AdministratorsTeamId, Ids(await ReadHttpPageAsync(owner,
                    roleTeamsPath + "?search=" + scope.AdministratorsTeamId[..8]), "team_id"));
            }

            // Foreign parent IDs disclose no edges, even to the member who owns both tenants.
            await AssertEmptyHttpPageAsync(owner, TenantPath(tenantB) + "/teams/" +
                scopeA.AdministratorsTeamId + "/members");
            await AssertEmptyHttpPageAsync(owner, TenantPath(tenantB) + "/roles/" +
                scopeA.AdministrationRoleId + "/permissions");
            await AssertEmptyHttpPageAsync(owner, TenantPath(tenantB) + "/roles/" +
                scopeA.AdministrationRoleId + "/teams");

            await using (var mcp = await McpScenario.ConnectAsync(owner,
                             new Uri(owner.BaseAddress!, "/mcp")))
            {
                foreach (var scope in new[] { scopeA, scopeB })
                {
                    AssertIds(scope.TeamIds, await ReadTwoMcpPagesAsync(mcp,
                        "bdgrz.rbac.team.list", scope.TenantId, "team_id"));
                    AssertIds(scope.RoleIds, await ReadTwoMcpPagesAsync(mcp,
                        "bdgrz.rbac.role.list", scope.TenantId, "role_id"));
                    var members = await ReadMcpPageAsync(mcp, "bdgrz.rbac.team-member.list",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = scope.TenantId,
                            ["team_id"] = scope.AdministratorsTeamId,
                            ["search"] = scope.MemberId[..8],
                        });
                    Assert.Contains(scope.MemberId, Ids(members, "member_id"));
                    var permissions = await ReadMcpPageAsync(mcp,
                        "bdgrz.rbac.role-permission.list", new Dictionary<string, object?>
                        {
                            ["tenant_id"] = scope.TenantId,
                            ["role_id"] = scope.AdministrationRoleId,
                            ["search"] = "manage",
                        });
                    Assert.NotEmpty(Ids(permissions, "permission"));
                    Assert.All(Ids(permissions, "role_id"),
                        roleId => Assert.Equal(scope.AdministrationRoleId, roleId));
                    var roleTeams = await ReadMcpPageAsync(mcp, "bdgrz.rbac.role-team.list",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = scope.TenantId,
                            ["role_id"] = scope.AdministrationRoleId,
                            ["search"] = scope.AdministratorsTeamId[..8],
                        });
                    Assert.Contains(scope.AdministratorsTeamId, Ids(roleTeams, "team_id"));
                }
                foreach (var (tool, parentKey, parentId) in new[]
                         {
                             ("bdgrz.rbac.team-member.list", "team_id", scopeA.AdministratorsTeamId),
                             ("bdgrz.rbac.role-permission.list", "role_id", scopeA.AdministrationRoleId),
                             ("bdgrz.rbac.role-team.list", "role_id", scopeA.AdministrationRoleId),
                         })
                {
                    var foreign = await ReadMcpPageAsync(mcp, tool, new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenantB.ToString(),
                        [parentKey] = parentId,
                    });
                    Assert.Empty(foreign.GetProperty("items").EnumerateArray());
                }
            }

            // An unrelated authenticated user gets the same denial for every RBAC list family.
            foreach (var path in new[]
                     {
                         TenantPath(tenantA) + "/teams",
                         TenantPath(tenantA) + "/roles",
                         TenantPath(tenantA) + "/teams/" + scopeA.AdministratorsTeamId + "/members",
                         TenantPath(tenantA) + "/roles/" + scopeA.AdministrationRoleId + "/permissions",
                         TenantPath(tenantA) + "/roles/" + scopeA.AdministrationRoleId + "/teams",
                     })
            {
                using var denied = await outsider.GetAsync(path);
                Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            }
            await using var outsiderMcp = await McpScenario.ConnectAsync(outsider,
                new Uri(outsider.BaseAddress!, "/mcp"));
            foreach (var (tool, parentKey, parentId) in new[]
                     {
                         ("bdgrz.rbac.team.list", "", ""),
                         ("bdgrz.rbac.role.list", "", ""),
                         ("bdgrz.rbac.team-member.list", "team_id", scopeA.AdministratorsTeamId),
                         ("bdgrz.rbac.role-permission.list", "role_id", scopeA.AdministrationRoleId),
                         ("bdgrz.rbac.role-team.list", "role_id", scopeA.AdministrationRoleId),
                     })
            {
                var input = new Dictionary<string, object?> { ["tenant_id"] = tenantA.ToString() };
                if (parentKey.Length > 0)
                    input[parentKey] = parentId;
                _ = await outsiderMcp.When(tool, input).ExpectFailure("NotFound");
            }
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ShouldScopeImportCountsRowsAndPreviewGivenTwoTenants(bool splitHosts) =>
        RunWithHostsAsync(splitHosts, async (owner, outsider) =>
        {
            // Arrange
            await TenantInvitationE2ETests.LoginAsync(owner,
                "import-matrix-owner-" + Guid.NewGuid().ToString("N") + "@example.com");
            await TenantInvitationE2ETests.LoginAsync(outsider,
                "import-matrix-outsider-" + Guid.NewGuid().ToString("N") + "@example.com");
            var tenantA = await CreateTenantAsync(owner, "Import matrix A");
            var tenantB = await CreateTenantAsync(owner, "Import matrix B");
            var importsA = TenantPath(tenantA) + "/application-imports";
            var importsB = TenantPath(tenantB) + "/application-imports";
            var batchA = await StageImportAsync(owner, importsA, "A",
                [ImportRow("A-1", "A one"), ImportRow("A-2", "A two"),
                    ImportRow("A-3", " ")]);
            var batchB = await StageImportAsync(owner, importsB, "B",
                [ImportRow("B-1", "B one")]);
            var pathA = importsA + "/" + batchA;
            var pathB = importsB + "/" + batchB;
            var projectedA = await WaitForJsonAsync(owner, pathA,
                document => document.GetProperty("row_count").GetInt32() == 3);
            var projectedB = await WaitForJsonAsync(owner, pathB,
                document => document.GetProperty("row_count").GetInt32() == 1);

            // Act and assert: the batch's counters are tenant-local.
            AssertImportCounts(projectedA, tenantA, batchA, rowCount: 3, invalidCount: 1);
            AssertImportCounts(projectedB, tenantB, batchB, rowCount: 1, invalidCount: 0);
            var firstA = await ReadHttpPageAsync(owner, pathA + "/rows?limit=1");
            var cursorA = firstA.GetProperty("next_cursor").GetString();
            Assert.NotNull(cursorA);
            var secondA = await ReadHttpPageAsync(owner, pathA + "/rows?limit=1&cursor=" +
                Uri.EscapeDataString(cursorA));
            Assert.All(Ids(firstA, "source_record_id"), id => Assert.StartsWith("A-", id));
            Assert.All(Ids(secondA, "source_record_id"), id => Assert.StartsWith("A-", id));
            var rowsB = await ReadHttpPageAsync(owner, pathB + "/rows");
            Assert.Equal("B-1", Assert.Single(Ids(rowsB, "source_record_id")));
            var previewA = await ReadHttpPageAsync(owner, pathA + "/preview");
            var previewB = await ReadHttpPageAsync(owner, pathB + "/preview");
            Assert.Equal(3, Ids(previewA, "source_record_id").Length);
            Assert.All(Ids(previewA, "source_record_id"), id => Assert.StartsWith("A-", id));
            Assert.Equal("B-1", Assert.Single(Ids(previewB, "source_record_id")));

            foreach (var suffix in ImportReadSuffixes)
            {
                using var cross = await owner.GetAsync(importsB + "/" + batchA + suffix);
                using var absent = await owner.GetAsync(importsB + "/" + Guid.NewGuid() + suffix);
                using var outsiderRead = await outsider.GetAsync(pathA + suffix);
                Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);
                Assert.Equal(cross.StatusCode, absent.StatusCode);
                Assert.Equal(cross.StatusCode, outsiderRead.StatusCode);
            }
            using (var foreignCursor = await owner.GetAsync(pathB + "/rows?limit=1&cursor=" +
                       Uri.EscapeDataString(cursorA)))
                Assert.Equal(HttpStatusCode.BadRequest, foreignCursor.StatusCode);

            await using (var mcp = await McpScenario.ConnectAsync(owner,
                             new Uri(owner.BaseAddress!, "/mcp")))
            {
                foreach (var (tenant, batch, rows) in new[]
                         {
                             (tenantA, batchA, 3),
                             (tenantB, batchB, 1),
                         })
                {
                    var input = new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenant.ToString(),
                        ["batch_id"] = batch,
                    };
                    var view = await ReadMcpResultAsync(mcp, "bdgrz.application_import.get", input);
                    AssertImportCounts(view, tenant, batch, rows, rows == 3 ? 1 : 0);
                    var rowPage = await ReadMcpPageAsync(mcp,
                        "bdgrz.application_import.rows.list", input);
                    var previewPage = await ReadMcpPageAsync(mcp,
                        "bdgrz.application_import.preview", input);
                    Assert.Equal(rows, Ids(rowPage, "source_record_id").Length);
                    Assert.Equal(rows, Ids(previewPage, "source_record_id").Length);
                    Assert.All(Ids(rowPage, "tenant_id"),
                        id => Assert.Equal(tenant.ToString(), id));
                    Assert.All(Ids(previewPage, "tenant_id"),
                        id => Assert.Equal(tenant.ToString(), id));
                }
                foreach (var tool in ImportReadTools)
                    _ = await mcp.When(tool, new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenantB.ToString(),
                        ["batch_id"] = batchA,
                    }).ExpectFailure("NotFound");
            }
            await using var outsiderMcp = await McpScenario.ConnectAsync(outsider,
                new Uri(outsider.BaseAddress!, "/mcp"));
            foreach (var tool in ImportReadTools)
                _ = await outsiderMcp.When(tool, new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantA.ToString(),
                    ["batch_id"] = batchA,
                }).ExpectFailure("NotFound");
        });

    async Task RunWithHostsAsync(bool splitHosts, Func<HttpClient, HttpClient, Task> exercise)
    {
        var applicationName = "compliance-read-matrix-" + Guid.NewGuid().ToString("N");
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
            var previousMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
            HttpClient owner;
            HttpClient outsider;
            try
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE",
                    splitHosts ? "api" : "standalone");
                owner = factory.CreateClient();
                outsider = factory.CreateClient();
            }
            finally
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", previousMode);
            }
            using (owner)
            using (outsider)
                await exercise(owner, outsider);
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

    static async Task<TenantRbacScope> SeedRbacAsync(HttpClient owner, Uuid tenantId,
        Uuid userId, string namePrefix)
    {
        var teamsPath = TenantPath(tenantId) + "/teams";
        var rolesPath = TenantPath(tenantId) + "/roles";
        var administratorsTeam = BuiltInRbac.AdministratorsTeamId(tenantId).ToString();
        var administrationRole = BuiltInRbac.TenantAdministrationRoleId(tenantId).ToString();
        _ = await WaitForPageAsync(owner, teamsPath,
            page => Ids(page, "team_id").Contains(administratorsTeam));
        var teamIds = new[] { Uuid.CreateVersion4().ToString(), Uuid.CreateVersion4().ToString() };
        var roleIds = new[] { Uuid.CreateVersion4().ToString(), Uuid.CreateVersion4().ToString() };
        for (var index = 0; index < 2; index++)
        {
            await PostUntilNoContentAsync(owner, teamsPath + "/" + teamIds[index],
                new { name = namePrefix + "-team-" + index });
            await PostUntilNoContentAsync(owner, rolesPath + "/" + roleIds[index],
                new { name = namePrefix + "-role-" + index });
        }
        _ = await WaitForPageAsync(owner, teamsPath + "?search=Matrix",
            page => Ids(page, "team_id").Length == 2);
        _ = await WaitForPageAsync(owner, rolesPath + "?search=Matrix",
            page => Ids(page, "role_id").Length == 2);
        return new TenantRbacScope(tenantId, teamIds, roleIds,
            administratorsTeam, administrationRole, RbacIds.Member(tenantId, userId).ToString());
    }

    static async Task<Uuid> CreateTenantAsync(HttpClient owner, string name)
    {
        using var response = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name,
            slug = "read-matrix-" + Guid.NewGuid().ToString("N")[..12],
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Uuid.Parse((await ReadJsonAsync(response)).GetProperty("tenant_id").GetString()!,
            CultureInfo.InvariantCulture);
    }

    static async Task<string> StageImportAsync(HttpClient owner, string importsPath,
        string sourceNamespace, object[] rows)
    {
        var body = new
        {
            submission_id = Guid.NewGuid(),
            source_key = "matrix",
            source_namespace = sourceNamespace,
            coverage = "partial",
            rows,
        };
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(importsPath, body);
            if (response.StatusCode == HttpStatusCode.OK)
                return (await ReadJsonAsync(response)).GetProperty("batch_id").GetString()!;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("Import staging remained unauthorized after tenant bootstrap.");
    }

    static object ImportRow(string sourceId, string name) => new
    {
        source_record_id = sourceId,
        name,
        purpose = "Leak matrix",
        owner_reference = (string?)null,
    };

    static async Task PostUntilNoContentAsync(HttpClient client, string path, object body)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.PostAsJsonAsync(path, body);
            if (response.StatusCode == HttpStatusCode.NoContent)
                return;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("RBAC management remained unauthorized after tenant bootstrap.");
    }

    static async Task<JsonElement> WaitForPageAsync(HttpClient client, string path,
        Func<JsonElement, bool> ready) => await WaitForJsonAsync(client, path, ready);

    static async Task<JsonElement> WaitForJsonAsync(HttpClient client, string path,
        Func<JsonElement, bool> ready)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var document = await ReadJsonAsync(response);
                if (ready(document))
                    return document;
            }
            else
                Assert.True(response.StatusCode is HttpStatusCode.NotFound or
                    HttpStatusCode.Forbidden or HttpStatusCode.Conflict,
                    await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("The tenant read projection did not reach the expected state: " + path);
    }

    static async Task<JsonElement> ReadHttpPageAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    static async Task AssertEmptyHttpPageAsync(HttpClient client, string path) =>
        Assert.Empty((await ReadHttpPageAsync(client, path)).GetProperty("items").EnumerateArray());

    static async Task<IReadOnlyList<string>> ReadTwoHttpPagesAsync(HttpClient client, string firstPath,
        string property)
    {
        var first = await ReadHttpPageAsync(client, firstPath);
        var firstIds = Ids(first, property);
        Assert.Single(firstIds);
        var cursor = first.GetProperty("next_cursor").GetString();
        Assert.NotNull(cursor);
        var second = await ReadHttpPageAsync(client, firstPath + "&cursor=" +
            Uri.EscapeDataString(cursor));
        return [.. firstIds, .. Ids(second, property)];
    }

    static async Task<IReadOnlyList<string>> ReadTwoMcpPagesAsync(McpScenario mcp, string tool,
        Uuid tenantId, string property)
    {
        var input = new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId.ToString(),
            ["search"] = "Matrix",
            ["limit"] = 1,
        };
        var first = await ReadMcpPageAsync(mcp, tool, input);
        var firstIds = Ids(first, property);
        Assert.Single(firstIds);
        var cursor = first.GetProperty("next_cursor").GetString();
        Assert.NotNull(cursor);
        input["cursor"] = cursor;
        var second = await ReadMcpPageAsync(mcp, tool, input);
        return [.. firstIds, .. Ids(second, property)];
    }

    static async Task<JsonElement> ReadMcpPageAsync(McpScenario mcp, string tool,
        Dictionary<string, object?> input) => await ReadMcpResultAsync(mcp, tool, input);

    static async Task<JsonElement> ReadMcpResultAsync(McpScenario mcp, string tool,
        Dictionary<string, object?> input)
    {
        var call = await mcp.When(tool, input).ExpectSuccess();
        return Assert.IsType<JsonElement>(call.StructuredJson).GetProperty("result");
    }

    static string[] Ids(JsonElement page, string property) =>
        page.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty(property).GetString()!).ToArray();

    static void AssertIds(IReadOnlyList<string> expected, IReadOnlyList<string> actual) =>
        Assert.Equal(expected.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));

    static void AssertImportCounts(JsonElement view, Uuid tenantId, string batchId,
        int rowCount, int invalidCount)
    {
        Assert.Equal(tenantId.ToString(), view.GetProperty("tenant_id").GetString());
        Assert.Equal(batchId, view.GetProperty("batch_id").GetString());
        Assert.Equal(rowCount, view.GetProperty("row_count").GetInt32());
        Assert.Equal(invalidCount, view.GetProperty("invalid_count").GetInt32());
        foreach (var name in new[] { "pending_count", "applied_count", "skipped_count", "failed_count" })
            Assert.Equal(0, view.GetProperty(name).GetInt32());
    }

    static string TenantPath(Uuid tenantId) => "/api/v1/tenants/" + tenantId;

    static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync())).RootElement.Clone();

    sealed record TenantRbacScope(Uuid TenantId, IReadOnlyList<string> TeamIds,
        IReadOnlyList<string> RoleIds, string AdministratorsTeamId, string AdministrationRoleId,
        string MemberId);
}
