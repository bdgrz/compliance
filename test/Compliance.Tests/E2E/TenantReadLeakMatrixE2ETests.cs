using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     EN-01 current-surface leak matrix: two real tenants with a shared owner, additional
///     tenant members, and an outsider exercise tenant-owned pages through HTTP and MCP in both
///     host modes.
/// </summary>
[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class TenantReadLeakMatrixE2ETests(BrokerStackFixture broker)
{
    static readonly string[] ImportReadSuffixes = ["", "/rows", "/preview"];
    static readonly string[] ImportCursorTools =
    [
        "bdgrz.application_import.rows.list",
        "bdgrz.application_import.preview",
    ];
    static readonly string[] ImportSourceIdsA = ["A-1", "A-2", "A-3"];
    static readonly string[] ImportSourceIdsB = ["B-1"];
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
        RunWithHostsAndServicesAsync(splitHosts, async (owner, outsider, factory, worker) =>
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
            var invitedMemberA = await InviteAndAssignTeamMemberAsync(factory, worker,
                splitHosts, owner, tenantA, scopeA.AdministratorsTeamId);
            var invitedMemberB = await InviteAndAssignTeamMemberAsync(factory, worker,
                splitHosts, owner, tenantB, scopeB.AdministratorsTeamId);
            var expectedMemberIdsA = new List<string> { scopeA.MemberId, invitedMemberA.MemberId };
            var expectedMemberIdsB = new List<string> { scopeB.MemberId, invitedMemberB.MemberId };
            var memberPagesByTenant = new Dictionary<Uuid, IReadOnlyList<JsonElement>>();

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
                var expectedMemberIds = scope.TenantId == tenantA
                    ? expectedMemberIdsA
                    : expectedMemberIdsB;
                var members = await WaitForPageAsync(owner, membersPath,
                    page => expectedMemberIds.All(id => Ids(page, "member_id").Contains(id)));
                Assert.Contains(scope.MemberId, Ids(members, "member_id"));
                Assert.All(Ids(members, "team_id"),
                    teamId => Assert.Equal(scope.AdministratorsTeamId, teamId));
                Assert.Contains(scope.MemberId, Ids(await ReadHttpPageAsync(owner,
                    membersPath + "?search=" + scope.MemberId[..8]), "member_id"));
                var memberPages = await ReadAllHttpPagesAsync(owner, membersPath + "?limit=1");
                Assert.True(memberPages.Count >= 2);
                AssertIds(expectedMemberIds,
                    memberPages.SelectMany(page => Ids(page, "member_id")).ToArray());
                Assert.All(memberPages, page => Assert.Single(Ids(page, "member_id")));
                Assert.All(memberPages.SelectMany(page => Ids(page, "team_id")),
                    teamId => Assert.Equal(scope.AdministratorsTeamId, teamId));
                memberPagesByTenant[scope.TenantId] = memberPages;

                var permissions = await WaitForPageAsync(owner,
                    permissionsPath + "?search=manage",
                    page => Ids(page, "permission").Length >= 2);
                var permissionPages = await ReadAllHttpPagesAsync(owner,
                    permissionsPath + "?search=manage&limit=1");
                AssertIds(Ids(permissions, "permission"),
                    permissionPages.SelectMany(page => Ids(page, "permission")).ToArray());
                Assert.All(permissionPages.SelectMany(page => Ids(page, "role_id")),
                    roleId => Assert.Equal(scope.AdministrationRoleId, roleId));

                var roleTeams = await WaitForPageAsync(owner, roleTeamsPath,
                    page => scope.RoleTeamIds.All(id => Ids(page, "team_id").Contains(id)));
                Assert.Contains(scope.AdministratorsTeamId, Ids(roleTeams, "team_id"));
                Assert.All(Ids(roleTeams, "role_id"),
                    roleId => Assert.Equal(scope.AdministrationRoleId, roleId));
                var roleTeamPages = await ReadAllHttpPagesAsync(owner,
                    roleTeamsPath + "?limit=1");
                Assert.True(roleTeamPages.Count >= 2);
                AssertIds(scope.RoleTeamIds,
                    roleTeamPages.SelectMany(page => Ids(page, "team_id")).ToArray());
                Assert.All(roleTeamPages.SelectMany(page => Ids(page, "role_id")),
                    roleId => Assert.Equal(scope.AdministrationRoleId, roleId));
                Assert.Contains(scope.AdministratorsTeamId, Ids(await ReadHttpPageAsync(owner,
                    roleTeamsPath + "?search=" + scope.AdministratorsTeamId[..8]), "team_id"));

                // The team-keyed view of the same assignments lists the team's roles in one read.
                var teamRoles = await WaitForPageAsync(owner,
                    TenantPath(scope.TenantId) + "/teams/" + scope.AdministratorsTeamId + "/roles",
                    page => Ids(page, "role_id").Contains(scope.AdministrationRoleId));
                Assert.All(Ids(teamRoles, "team_id"),
                    teamId => Assert.Equal(scope.AdministratorsTeamId, teamId));
            }

            var memberCursorA = memberPagesByTenant[tenantA][0]
                .GetProperty("next_cursor").GetString();
            Assert.NotNull(memberCursorA);
            using (var foreignMemberCursor = await owner.GetAsync(TenantPath(tenantB) +
                       "/teams/" + scopeB.AdministratorsTeamId + "/members?limit=1&cursor=" +
                       Uri.EscapeDataString(memberCursorA)))
                Assert.Equal(HttpStatusCode.BadRequest, foreignMemberCursor.StatusCode);

            // Foreign parent IDs disclose no edges, even to the member who owns both tenants.
            await AssertEmptyHttpPageAsync(owner, TenantPath(tenantB) + "/teams/" +
                scopeA.AdministratorsTeamId + "/members");
            await AssertEmptyHttpPageAsync(owner, TenantPath(tenantB) + "/roles/" +
                scopeA.AdministrationRoleId + "/permissions");
            await AssertEmptyHttpPageAsync(owner, TenantPath(tenantB) + "/roles/" +
                scopeA.AdministrationRoleId + "/teams");
            var roleTeamCursor = await ReadHttpPageAsync(owner,
                TenantPath(tenantA) + "/roles/" + scopeA.AdministrationRoleId + "/teams?limit=1");
            var cursorAForRoleTeams = roleTeamCursor.GetProperty("next_cursor").GetString();
            Assert.NotNull(cursorAForRoleTeams);
            using (var foreignRoleTeamCursor = await owner.GetAsync(TenantPath(tenantB) +
                       "/roles/" + scopeB.AdministrationRoleId + "/teams?limit=1&cursor=" +
                       Uri.EscapeDataString(cursorAForRoleTeams)))
                Assert.Equal(HttpStatusCode.BadRequest, foreignRoleTeamCursor.StatusCode);

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
                    var memberPages = await ReadAllMcpPagesAsync(mcp,
                        "bdgrz.rbac.team-member.list", new Dictionary<string, object?>
                        {
                            ["tenant_id"] = scope.TenantId,
                            ["team_id"] = scope.AdministratorsTeamId,
                            ["limit"] = 1,
                        });
                    Assert.True(memberPages.Count >= 2);
                    var expectedMemberIds = scope.TenantId == tenantA
                        ? expectedMemberIdsA
                        : expectedMemberIdsB;
                    AssertIds(expectedMemberIds,
                        memberPages.SelectMany(page => Ids(page, "member_id")).ToArray());
                    Assert.All(memberPages, page => Assert.Single(Ids(page, "member_id")));
                    Assert.All(memberPages.SelectMany(page => Ids(page, "team_id")),
                        teamId => Assert.Equal(scope.AdministratorsTeamId, teamId));
                    if (scope.TenantId == tenantA)
                    {
                        var memberCursor = memberPages[0].GetProperty("next_cursor").GetString();
                        Assert.NotNull(memberCursor);
                        _ = await mcp.When("bdgrz.rbac.team-member.list",
                            new Dictionary<string, object?>
                            {
                                ["tenant_id"] = tenantB.ToString(),
                                ["team_id"] = scopeB.AdministratorsTeamId,
                                ["limit"] = 1,
                                ["cursor"] = memberCursor,
                            }).ExpectFailure("Validation");
                    }
                    var permissionInput = new Dictionary<string, object?>
                    {
                        ["tenant_id"] = scope.TenantId,
                        ["role_id"] = scope.AdministrationRoleId,
                        ["search"] = "manage",
                        ["limit"] = 1,
                    };
                    var permissionPages = await ReadAllMcpPagesAsync(mcp,
                        "bdgrz.rbac.role-permission.list", permissionInput);
                    Assert.True(permissionPages.Count >= 2);
                    Assert.All(permissionPages.SelectMany(page => Ids(page, "role_id")),
                        roleId => Assert.Equal(scope.AdministrationRoleId, roleId));
                    var roleTeams = await ReadMcpPageAsync(mcp, "bdgrz.rbac.role-team.list",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = scope.TenantId,
                            ["role_id"] = scope.AdministrationRoleId,
                            ["search"] = scope.AdministratorsTeamId[..8],
                        });
                    Assert.Contains(scope.AdministratorsTeamId, Ids(roleTeams, "team_id"));
                    var roleTeamPages = await ReadAllMcpPagesAsync(mcp,
                        "bdgrz.rbac.role-team.list", new Dictionary<string, object?>
                        {
                            ["tenant_id"] = scope.TenantId,
                            ["role_id"] = scope.AdministrationRoleId,
                            ["limit"] = 1,
                        });
                    Assert.True(roleTeamPages.Count >= 2);
                    AssertIds(scope.RoleTeamIds,
                        roleTeamPages.SelectMany(page => Ids(page, "team_id")).ToArray());
                    Assert.All(roleTeamPages.SelectMany(page => Ids(page, "role_id")),
                        roleId => Assert.Equal(scope.AdministrationRoleId, roleId));
                    if (scope == scopeA)
                        _ = await mcp.When("bdgrz.rbac.role-team.list",
                            new Dictionary<string, object?>
                            {
                                ["tenant_id"] = scopeB.TenantId.ToString(),
                                ["role_id"] = scopeB.AdministrationRoleId,
                                ["limit"] = 1,
                                ["cursor"] = roleTeamPages[0].GetProperty("next_cursor").GetString(),
                            }).ExpectFailure("Validation");
                }
                foreach (var (tool, parentKey, parentId) in new[]
                         {
                             ("bdgrz.rbac.team-member.list", "team_id", scopeA.AdministratorsTeamId),
                             ("bdgrz.rbac.role-permission.list", "role_id", scopeA.AdministrationRoleId),
                             ("bdgrz.rbac.role-team.list", "role_id", scopeA.AdministrationRoleId),
                             ("bdgrz.rbac.team-role.list", "team_id", scopeA.AdministratorsTeamId),
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
                         TenantPath(tenantA) + "/teams/" + scopeA.AdministratorsTeamId + "/roles",
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
                         ("bdgrz.rbac.team-role.list", "team_id", scopeA.AdministratorsTeamId),
                     })
            {
                var input = new Dictionary<string, object?> { ["tenant_id"] = tenantA.ToString() };
                if (parentKey.Length > 0)
                    input[parentKey] = parentId;
                _ = await outsiderMcp.When(tool, input).ExpectFailure("NotFound");
            }

            // A suspended member's open work remains visible only to an administrator of
            // the owning tenant, including when the API and worker run independently.
            var boundaryId = Uuid.CreateVersion4();
            var versionId = Uuid.CreateVersion4();
            var boundary = new SystemBoundary(tenantA, boundaryId);
            Assert.True(boundary.Create(Uuid.CreateVersion4(), versionId,
                new BoundaryContent("Member work", "readiness", ["security"], []),
                userId, "Administrator", DateTimeOffset.UtcNow).IsSuccess);
            using (var scope = factory.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<IAggregateWriter>().SaveAsync(
                    boundary, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            using (var assigned = await owner.PostAsJsonAsync(TenantPath(tenantA) + "/responsibilities", new
            {
                member_user_id = invitedMemberA.UserId,
                type = "control_owner",
                record_type = "boundary",
                record_id = boundaryId,
                version_id = versionId,
                scope_revision = 1,
                effective_from = DateTimeOffset.UtcNow.AddMinutes(-1),
                effective_until = (DateTimeOffset?)null,
                separation_of_duties_waiver_ids = Array.Empty<string>(),
            }))
                Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);

            var responsibilitiesA = TenantPath(tenantA) + "/members/" +
                                    invitedMemberA.UserId + "/responsibilities";
            var responsibilitiesB = TenantPath(tenantB) + "/members/" +
                                    invitedMemberA.UserId + "/responsibilities";
            var openWork = await MemberResponsibilityReadiness.WaitForAsync(
                worker?.Services ?? factory.Services, owner,
                tenantA, boundaryId, versionId,
                Uuid.Parse(invitedMemberA.MemberId, CultureInfo.InvariantCulture), responsibilitiesA);
            Assert.Equal(tenantA.ToString(), openWork[0].GetProperty("tenant_id").GetString());
            using (var foreignMember = await owner.GetAsync(TenantPath(tenantB) +
                       "/members/" + invitedMemberA.UserId))
                Assert.Equal(HttpStatusCode.NotFound, foreignMember.StatusCode);
            using (var foreignWork = await owner.GetAsync(responsibilitiesB))
            {
                Assert.Equal(HttpStatusCode.OK, foreignWork.StatusCode);
                Assert.Empty((await ReadJsonAsync(foreignWork)).EnumerateArray());
            }

            using (var suspended = await owner.PostAsJsonAsync(TenantPath(tenantA) +
                       "/members/" + invitedMemberA.UserId + "/suspensions",
                       new { reason = "Reassign open work." }))
                Assert.Equal(HttpStatusCode.NoContent, suspended.StatusCode);
            var suspendedMember = await WaitForJsonAsync(owner,
                TenantPath(tenantA) + "/members/" + invitedMemberA.UserId,
                result => result.GetProperty("is_suspended").GetBoolean());
            Assert.True(suspendedMember.GetProperty("is_suspended").GetBoolean());
            using (var orphaned = await owner.GetAsync(responsibilitiesA))
            {
                Assert.Equal(HttpStatusCode.OK, orphaned.StatusCode);
                var work = await ReadJsonAsync(orphaned);
                Assert.Equal(openWork[0].GetProperty("assignment_id").GetString(),
                    Assert.Single(work.EnumerateArray()).GetProperty("assignment_id").GetString());
            }
            using (var outsiderRead = await outsider.GetAsync(responsibilitiesA))
                Assert.Equal(HttpStatusCode.NotFound, outsiderRead.StatusCode);
            using (var outsiderMember = await outsider.GetAsync(TenantPath(tenantA) +
                       "/members/" + invitedMemberA.UserId))
                Assert.Equal(HttpStatusCode.NotFound, outsiderMember.StatusCode);
            await using (var mcp = await McpScenario.ConnectAsync(owner,
                             new Uri(owner.BaseAddress!, "/mcp")))
            {
                _ = await mcp.When("bdgrz.member.responsibilities.list",
                    new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenantA.ToString(),
                        ["user_id"] = invitedMemberA.UserId,
                    }).ExpectSuccess();
                _ = await mcp.When("bdgrz.tenant.member.get", new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantA.ToString(),
                    ["user_id"] = invitedMemberA.UserId,
                }).ExpectSuccess();
            }
            _ = await outsiderMcp.When("bdgrz.member.responsibilities.list",
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantA.ToString(),
                    ["user_id"] = invitedMemberA.UserId,
                }).ExpectFailure("NotFound");
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
            var httpRowsA = await ReadAllHttpPagesAsync(owner, pathA + "/rows?limit=1");
            var cursorA = httpRowsA[0].GetProperty("next_cursor").GetString();
            Assert.NotNull(cursorA);
            AssertIds(["A-1", "A-2", "A-3"],
                httpRowsA.SelectMany(page => Ids(page, "source_record_id")).ToArray());
            Assert.All(httpRowsA, page => Assert.True(
                Ids(page, "source_record_id").Length <= 1));
            Assert.All(httpRowsA.SelectMany(page => page.GetProperty("items").EnumerateArray()),
                row =>
                {
                    Assert.Equal(tenantA.ToString(), row.GetProperty("tenant_id").GetString());
                    Assert.Equal(batchA, row.GetProperty("batch_id").GetString());
                });
            var rowsB = await ReadHttpPageAsync(owner, pathB + "/rows");
            Assert.Equal("B-1", Assert.Single(Ids(rowsB, "source_record_id")));
            Assert.Null(rowsB.GetProperty("next_cursor").GetString());
            var previewPagesA = await ReadAllHttpPagesAsync(owner,
                pathA + "/preview?limit=1");
            var previewB = await ReadHttpPageAsync(owner, pathB + "/preview");
            AssertIds(ImportSourceIdsA, previewPagesA
                .SelectMany(page => Ids(page, "source_record_id")).ToArray());
            Assert.All(previewPagesA, page => Assert.True(
                Ids(page, "source_record_id").Length <= 1));
            Assert.All(previewPagesA.SelectMany(page => page.GetProperty("items").EnumerateArray()),
                row => Assert.Equal(tenantA.ToString(), row.GetProperty("tenant_id").GetString()));
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
            var previewCursorA = previewPagesA[0].GetProperty("next_cursor").GetString();
            Assert.NotNull(previewCursorA);
            using (var foreignPreviewCursor = await owner.GetAsync(pathB + "/preview?limit=1&cursor=" +
                       Uri.EscapeDataString(previewCursorA)))
                Assert.Equal(HttpStatusCode.BadRequest, foreignPreviewCursor.StatusCode);

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
                    var rowPages = await ReadAllMcpPagesAsync(mcp,
                        "bdgrz.application_import.rows.list",
                        new Dictionary<string, object?>(input) { ["limit"] = 1 });
                    var previewPages = await ReadAllMcpPagesAsync(mcp,
                        "bdgrz.application_import.preview",
                        new Dictionary<string, object?>(input) { ["limit"] = 1 });
                    var expectedIds = tenant == tenantA ? ImportSourceIdsA : ImportSourceIdsB;
                    AssertIds(expectedIds,
                        rowPages.SelectMany(page => Ids(page, "source_record_id")).ToArray());
                    AssertIds(expectedIds,
                        previewPages.SelectMany(page => Ids(page, "source_record_id")).ToArray());
                    Assert.All(rowPages, page => Assert.True(
                        Ids(page, "source_record_id").Length <= 1));
                    Assert.All(previewPages, page => Assert.True(
                        Ids(page, "source_record_id").Length <= 1));
                    Assert.All(rowPages.SelectMany(page => Ids(page, "tenant_id")),
                        id => Assert.Equal(tenant.ToString(), id));
                    Assert.All(previewPages.SelectMany(page => Ids(page, "tenant_id")),
                        id => Assert.Equal(tenant.ToString(), id));
                }
                foreach (var tool in ImportCursorTools)
                {
                    var firstPage = await ReadMcpPageAsync(mcp, tool,
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = tenantA.ToString(),
                            ["batch_id"] = batchA,
                            ["limit"] = 1,
                        });
                    var cursor = firstPage.GetProperty("next_cursor").GetString();
                    Assert.NotNull(cursor);
                    _ = await mcp.When(tool, new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenantB.ToString(),
                        ["batch_id"] = batchB,
                        ["limit"] = 1,
                        ["cursor"] = cursor,
                    }).ExpectFailure("Validation");
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

    Task RunWithHostsAsync(bool splitHosts, Func<HttpClient, HttpClient, Task> exercise) =>
        RunWithHostsAndServicesAsync(splitHosts,
            (owner, outsider, _, _) => exercise(owner, outsider));

    async Task RunWithHostsAndServicesAsync(bool splitHosts,
        Func<HttpClient, HttpClient, WebApplicationFactory<Program>, IHost?, Task> exercise)
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
                await exercise(owner, outsider, factory, worker);
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
            await PostUntilNoContentAsync(owner,
                teamsPath + "/" + teamIds[index] + "/roles/" + administrationRole, new { });
        }
        _ = await WaitForPageAsync(owner, teamsPath + "?search=Matrix",
            page => Ids(page, "team_id").Length == 2);
        _ = await WaitForPageAsync(owner, rolesPath + "?search=Matrix",
            page => Ids(page, "role_id").Length == 2);
        return new TenantRbacScope(tenantId, teamIds, roleIds,
            administratorsTeam, administrationRole, RbacIds.Member(tenantId, userId).ToString(),
            new[] { administratorsTeam }.Concat(teamIds).OrderBy(id => id, StringComparer.Ordinal)
                .ToArray());
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
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
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
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
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

    static async Task<(string MemberId, string UserId)> InviteAndAssignTeamMemberAsync(
        WebApplicationFactory<Program> factory, IHost? worker, bool splitHosts,
        HttpClient owner, Uuid tenantId, string teamId)
    {
        var email = "read-matrix-member-" + Guid.NewGuid().ToString("N") + "@example.com";
        using (var invitation = await owner.PostAsJsonAsync(
                   TenantPath(tenantId) + "/invitations", new
                   {
                       email_address = email,
                       affiliation = "client_personnel",
                       administrator = false,
                   }))
            Assert.Equal(HttpStatusCode.NoContent, invitation.StatusCode);

        using var member = CreateClient(factory, splitHosts);
        var userId = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(member, email),
            CultureInfo.InvariantCulture);
        var services = worker?.Services ?? factory.Services;
        var delivery = services.GetRequiredService<MockTenantInvitationDelivery>();
        string? token = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline &&
               !delivery.TryGetLatest(tenantId, email, out token))
            await Task.Delay(250);
        Assert.NotNull(token);

        await TenantInvitationE2ETests.VerifyEmailAsync(factory, member, userId.ToString(), email,
            services.GetRequiredService<MockEmailChallengeDelivery>());
        using (var accepted = await member.PostAsJsonAsync(
                   TenantPath(tenantId) + "/invitations/acceptance", new
                   {
                       email_address = email,
                       token,
                   }))
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);

        var memberId = RbacIds.Member(tenantId, userId).ToString();
        await PostUntilNoContentAsync(owner, TenantPath(tenantId) + "/teams/" + teamId +
            "/members/" + memberId, new { });
        return (memberId, userId.ToString());
    }

    static HttpClient CreateClient(WebApplicationFactory<Program> factory, bool splitHosts)
    {
        var previousMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
        try
        {
            Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE",
                splitHosts ? "api" : "standalone");
            return factory.CreateClient();
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", previousMode);
        }
    }

    static async Task<JsonElement> WaitForPageAsync(HttpClient client, string path,
        Func<JsonElement, bool> ready) => await WaitForJsonAsync(client, path, ready);

    static async Task<JsonElement> WaitForJsonAsync(HttpClient client, string path,
        Func<JsonElement, bool> ready)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
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
        var pages = await ReadAllHttpPagesAsync(client, firstPath);
        Assert.True(pages.Count >= 2);
        Assert.All(pages, page => Assert.True(Ids(page, property).Length <= 1));
        return pages.SelectMany(page => Ids(page, property)).ToArray();
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
        var pages = await ReadAllMcpPagesAsync(mcp, tool, input);
        Assert.True(pages.Count >= 2);
        Assert.All(pages, page => Assert.True(Ids(page, property).Length <= 1));
        return pages.SelectMany(page => Ids(page, property)).ToArray();
    }

    static async Task<IReadOnlyList<JsonElement>> ReadAllHttpPagesAsync(HttpClient client,
        string firstPath)
    {
        var pages = new List<JsonElement>();
        string? cursor = null;
        do
        {
            var path = firstPath + (cursor is null ? string.Empty :
                "&cursor=" + Uri.EscapeDataString(cursor));
            var page = await ReadHttpPageAsync(client, path);
            pages.Add(page);
            Assert.True(pages.Count <= 50, "The permission cursor did not terminate.");
            cursor = page.GetProperty("next_cursor").GetString();
        } while (cursor is not null);
        return pages;
    }

    static async Task<IReadOnlyList<JsonElement>> ReadAllMcpPagesAsync(McpScenario mcp,
        string tool, Dictionary<string, object?> input)
    {
        var pages = new List<JsonElement>();
        string? cursor = null;
        do
        {
            if (cursor is not null)
                input["cursor"] = cursor;
            var page = await ReadMcpPageAsync(mcp, tool, input);
            pages.Add(page);
            Assert.True(pages.Count <= 50, "The permission cursor did not terminate.");
            cursor = page.GetProperty("next_cursor").GetString();
        } while (cursor is not null);
        return pages;
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
    }

    static string TenantPath(Uuid tenantId) => "/api/v1/tenants/" + tenantId;

    static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync())).RootElement.Clone();

    sealed record TenantRbacScope(Uuid TenantId, IReadOnlyList<string> TeamIds,
        IReadOnlyList<string> RoleIds, string AdministratorsTeamId, string AdministrationRoleId,
        string MemberId, IReadOnlyList<string> RoleTeamIds);
}
