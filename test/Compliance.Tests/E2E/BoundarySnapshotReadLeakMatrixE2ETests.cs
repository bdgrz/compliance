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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class BoundarySnapshotReadLeakMatrixE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    static readonly string[] SecurityCategory = ["security"];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ShouldScopeBoundaryAndSnapshotReadsGivenTwoTenants(bool splitHosts) =>
        RunWithHostsAsync(splitHosts, async (owner, reviewer, outsider, reviewerEmail, delivery) =>
        {
            // Arrange: one owner belongs to both tenants; the reviewer is a distinct human.
            var tenantA = await CreateTenantAsync(owner, "Boundary matrix A");
            var tenantB = await CreateTenantAsync(owner, "Boundary matrix B");
            var scopeA = await SeedAsync(owner, reviewer, reviewerEmail, delivery, tenantA, "A");
            var scopeB = await SeedAsync(owner, reviewer, reviewerEmail, delivery, tenantB, "B");

            // Act: read every current boundary and snapshot family over both transports.
            await using var mcp = await McpScenario.ConnectAsync(owner,
                new Uri(owner.BaseAddress!, "/mcp"));
            var tenantACursors = new Dictionary<string, CursorPair>();
            foreach (var scope in new[] { scopeA, scopeB })
            {
                var tenantPath = TenantPath(scope.TenantId);
                var programPath = tenantPath + "/programs/" + scope.ProgramId;
                var boundaryPath = tenantPath + "/boundaries/" + scope.BoundaryIds[0];
                var draftPath = tenantPath + "/boundaries/" + scope.BoundaryIds[1] +
                    "/drafts/" + scope.DraftVersionId;

                var httpBoundaries = await ReadHttpPagesAsync(owner,
                    programPath + "/boundaries", 2);
                var mcpBoundaries = await ReadMcpPagesAsync(mcp,
                    "bdgrz.boundary.program.list", new Dictionary<string, object?>
                    {
                        ["tenant_id"] = scope.TenantId.ToString(),
                        ["program_id"] = scope.ProgramId,
                    }, 2);
                AssertRows(scope, httpBoundaries.Rows, "boundary_id", scope.BoundaryIds);
                AssertRows(scope, mcpBoundaries.Rows, "boundary_id", scope.BoundaryIds);

                var httpVersions = await ReadHttpPagesAsync(owner,
                    boundaryPath + "/versions", 1);
                var mcpVersions = await ReadMcpPagesAsync(mcp,
                    "bdgrz.boundary.versions.list", new Dictionary<string, object?>
                    {
                        ["tenant_id"] = scope.TenantId.ToString(),
                        ["boundary_id"] = scope.BoundaryIds[0],
                    }, 1);
                AssertRows(scope, httpVersions.Rows, "version_id", [scope.ApprovedVersionId]);
                AssertRows(scope, mcpVersions.Rows, "version_id", [scope.ApprovedVersionId]);
                Assert.All(httpVersions.Rows, row =>
                    Assert.Equal("approved", row.GetProperty("status").GetString()));
                Assert.All(mcpVersions.Rows, row =>
                    Assert.Equal("approved", row.GetProperty("status").GetString()));

                var httpDecisions = await ReadHttpPagesAsync(owner,
                    boundaryPath + "/decisions", 2);
                var mcpDecisions = await ReadMcpPagesAsync(mcp,
                    "bdgrz.boundary.decisions.list", new Dictionary<string, object?>
                    {
                        ["tenant_id"] = scope.TenantId.ToString(),
                        ["boundary_id"] = scope.BoundaryIds[0],
                    }, 2);
                AssertRows(scope, httpDecisions.Rows, "decision_id", scope.DecisionIds);
                AssertRows(scope, mcpDecisions.Rows, "decision_id", scope.DecisionIds);

                var httpSnapshots = await ReadHttpPagesAsync(owner,
                    programPath + "/scope-snapshots", 2);
                var mcpSnapshots = await ReadMcpPagesAsync(mcp,
                    "bdgrz.snapshot.program.list", new Dictionary<string, object?>
                    {
                        ["tenant_id"] = scope.TenantId.ToString(),
                        ["program_id"] = scope.ProgramId,
                    }, 2);
                AssertRows(scope, httpSnapshots.Rows, "snapshot_id", scope.SnapshotIds);
                AssertRows(scope, mcpSnapshots.Rows, "snapshot_id", scope.SnapshotIds);
                Assert.All(httpSnapshots.Rows, row => AssertSnapshot(row, scope));
                Assert.All(mcpSnapshots.Rows, row => AssertSnapshot(row, scope));

                if (scope.TenantId == tenantA)
                {
                    tenantACursors.Add("boundaries", new CursorPair(
                        httpBoundaries.FirstCursor!, mcpBoundaries.FirstCursor!));
                    tenantACursors.Add("decisions", new CursorPair(
                        httpDecisions.FirstCursor!, mcpDecisions.FirstCursor!));
                    tenantACursors.Add("snapshots", new CursorPair(
                        httpSnapshots.FirstCursor!, mcpSnapshots.FirstCursor!));
                }

                await AssertReadPairAsync(owner, mcp, boundaryPath, "bdgrz.boundary.get",
                    new Dictionary<string, object?>
                    {
                        ["tenant_id"] = scope.TenantId.ToString(),
                        ["boundary_id"] = scope.BoundaryIds[0],
                    }, result =>
                    {
                        Assert.Equal(scope.BoundaryIds[0], result.GetProperty("boundary_id").GetString());
                        Assert.Equal(scope.ProgramId, result.GetProperty("program_id").GetString());
                        Assert.Equal(scope.ApprovedVersionId, result.GetProperty("latest_approved_version")
                            .GetProperty("version_id").GetString());
                    });
                await AssertReadPairAsync(owner, mcp, tenantPath + "/boundaries/" +
                    scope.BoundaryIds[1], "bdgrz.boundary.get", new Dictionary<string, object?>
                    {
                        ["tenant_id"] = scope.TenantId.ToString(),
                        ["boundary_id"] = scope.BoundaryIds[1],
                    }, result => Assert.Equal(scope.DraftVersionId,
                        result.GetProperty("draft").GetProperty("version_id").GetString()));
                await AssertReadPairAsync(owner, mcp, boundaryPath + "/versions/" +
                    scope.ApprovedVersionId, "bdgrz.boundary.version.get",
                    new Dictionary<string, object?>
                    {
                        ["tenant_id"] = scope.TenantId.ToString(),
                        ["boundary_id"] = scope.BoundaryIds[0],
                        ["version_id"] = scope.ApprovedVersionId,
                    }, result => Assert.Equal(scope.ApprovedVersionId,
                        result.GetProperty("version_id").GetString()));
                await AssertReadPairAsync(owner, mcp, boundaryPath +
                    "/effective-version?effective_on=2027-01-15",
                    "bdgrz.boundary.version.effective.get", new Dictionary<string, object?>
                    {
                        ["tenant_id"] = scope.TenantId.ToString(),
                        ["boundary_id"] = scope.BoundaryIds[0],
                        ["effective_on"] = "2027-01-15",
                    }, result => Assert.Equal(scope.ApprovedVersionId,
                        result.GetProperty("version_id").GetString()));
                foreach (var decisionId in scope.DecisionIds)
                    await AssertReadPairAsync(owner, mcp, boundaryPath + "/decisions/" +
                        decisionId, "bdgrz.boundary.decision.get",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = scope.TenantId.ToString(),
                            ["boundary_id"] = scope.BoundaryIds[0],
                            ["decision_id"] = decisionId,
                        }, result => Assert.Equal(decisionId,
                            result.GetProperty("decision_id").GetString()));
                await AssertReadPairAsync(owner, mcp, draftPath +
                    "/impact-preview?expected_revision=1", "bdgrz.boundary.impact.preview",
                    new Dictionary<string, object?>
                    {
                        ["tenant_id"] = scope.TenantId.ToString(),
                        ["boundary_id"] = scope.BoundaryIds[1],
                        ["draft_version_id"] = scope.DraftVersionId,
                        ["expected_revision"] = 1,
                    }, result =>
                    {
                        Assert.Equal(scope.BoundaryIds[1], result.GetProperty("boundary_id").GetString());
                        Assert.Equal(scope.DraftVersionId, result.GetProperty("draft_version_id").GetString());
                        Assert.NotEmpty(result.GetProperty("changes").EnumerateArray());
                        foreach (var contribution in result.GetProperty("contributions").EnumerateArray())
                            foreach (var record in contribution.GetProperty("records").EnumerateArray())
                                Assert.Equal(scope.TenantId.ToString(),
                                    record.GetProperty("tenant_id").GetString());
                    });
                foreach (var snapshotId in scope.SnapshotIds)
                {
                    await AssertReadPairAsync(owner, mcp, tenantPath + "/scope-snapshots/" +
                        snapshotId, "bdgrz.snapshot.get", new Dictionary<string, object?>
                        {
                            ["tenant_id"] = scope.TenantId.ToString(),
                            ["snapshot_id"] = snapshotId,
                        }, result => AssertSnapshot(result, scope));
                    await AssertReadPairAsync(owner, mcp, tenantPath + "/scope-snapshots/" +
                        snapshotId + "/verification", "bdgrz.snapshot.program_scope.verify",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = scope.TenantId.ToString(),
                            ["snapshot_id"] = snapshotId,
                        }, result =>
                        {
                            Assert.Equal(snapshotId, result.GetProperty("snapshot_id").GetString());
                            Assert.True(result.GetProperty("verified").GetBoolean());
                        });
                    await AssertReadPairAsync(owner, mcp, tenantPath + "/scope-snapshots/" +
                        snapshotId + "/manifest-regeneration",
                        "bdgrz.snapshot.program_scope.manifest_regenerate",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = scope.TenantId.ToString(),
                            ["snapshot_id"] = snapshotId,
                        }, result =>
                        {
                            Assert.Equal(snapshotId, result.GetProperty("snapshot_id").GetString());
                            Assert.Equal(64, result.GetProperty("content_sha256").GetString()?.Length);
                            using var manifest = JsonDocument.Parse(result.GetProperty("canonical_manifest")
                                .GetString()!);
                            Assert.Equal(scope.TenantId.ToString(), manifest.RootElement
                                .GetProperty("tenant_id").GetString());
                            Assert.Equal(scope.ProgramId, manifest.RootElement
                                .GetProperty("program_id").GetString());
                            Assert.Equal(scope.BoundaryIds[0], manifest.RootElement
                                .GetProperty("boundary_id").GetString());
                        });
                }
            }

            await AssertTransplantedCursorsAsync(owner, mcp, scopeB, tenantACursors);

            // Assert: foreign IDs and an unrelated authenticated user reveal none of these families.
            await AssertDeniedCasesAsync(owner, mcp, ReadCases(scopeA, tenantB));
            await using var outsiderMcp = await McpScenario.ConnectAsync(outsider,
                new Uri(outsider.BaseAddress!, "/mcp"));
            await AssertDeniedCasesAsync(outsider, outsiderMcp, ReadCases(scopeA, tenantA));
        });

    async Task RunWithHostsAsync(bool splitHosts,
        Func<HttpClient, HttpClient, HttpClient, string, MockTenantInvitationDelivery, Task> exercise)
    {
        var applicationName = $"compliance-boundary-snapshot-matrix-{Guid.NewGuid():N}";
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
            HttpClient reviewer;
            HttpClient outsider;
            try
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE",
                    splitHosts ? "api" : "standalone");
                owner = factory.CreateClient();
                reviewer = factory.CreateClient();
                outsider = factory.CreateClient();
            }
            finally
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", previousMode);
            }
            using (owner)
            using (reviewer)
            using (outsider)
            {
                await TenantInvitationE2ETests.LoginAsync(owner,
                    $"boundary-matrix-owner-{Guid.NewGuid():N}@example.com");
                await TenantInvitationE2ETests.LoginAsync(outsider,
                    $"boundary-matrix-outsider-{Guid.NewGuid():N}@example.com");
                var reviewerEmail = $"boundary-matrix-reviewer-{Guid.NewGuid():N}@example.com";
                var reviewerId = await TenantInvitationE2ETests.LoginAsync(reviewer, reviewerEmail);
                await TenantInvitationE2ETests.VerifyEmailAsync(factory, reviewer,
                    reviewerId, reviewerEmail,
                    splitHosts ? worker!.Services.GetRequiredService<MockEmailChallengeDelivery>() : null);
                var delivery = splitHosts
                    ? worker!.Services.GetRequiredService<MockTenantInvitationDelivery>()
                    : factory.Services.GetRequiredService<MockTenantInvitationDelivery>();
                await exercise(owner, reviewer, outsider, reviewerEmail, delivery);
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

    static async Task<TenantScope> SeedAsync(HttpClient owner, HttpClient reviewer,
        string reviewerEmail, MockTenantInvitationDelivery delivery, Uuid tenantId, string marker)
    {
        var programId = await CreateProgramAsync(owner, tenantId, marker + " program");
        await GrantReviewerAsync(owner, reviewer, reviewerEmail, delivery, tenantId);
        var programPath = TenantPath(tenantId) + "/programs/" + programId;
        var first = await CreateBoundaryAsync(owner, tenantId, programPath,
            marker + " approved boundary");
        var firstPath = TenantPath(tenantId) + "/boundaries/" + first.BoundaryId;
        var preview = await WaitForAsync(owner, firstPath + "/drafts/" + first.VersionId +
            "/impact-preview?expected_revision=1", static result =>
                result.GetProperty("complete").GetBoolean());
        Assert.NotEmpty(preview.GetProperty("changes").EnumerateArray());
        var reviewId = await ReviewAsync(owner, reviewer, firstPath, first.VersionId);
        using (var approved = await reviewer.PostAsJsonAsync(firstPath + "/drafts/" +
                   first.VersionId + "/approvals", new
                   {
                       expected_revision = 1,
                       accepted_review_decision_id = reviewId,
                       effective_from = "2027-01-01",
                       rationale = "Approved for the tenant isolation proof.",
                       impact_digest = preview.GetProperty("digest").GetString(),
                   }))
            Assert.Equal(HttpStatusCode.NoContent, approved.StatusCode);
        var approvedView = await WaitForAsync(owner, firstPath, static result =>
            result.GetProperty("latest_approved_version").ValueKind == JsonValueKind.Object);
        var approvalId = approvedView.GetProperty("latest_decision")
            .GetProperty("decision_id").GetString()!;
        await WaitForPageCountAsync(owner, firstPath + "/decisions", 2);
        await WaitForPageCountAsync(owner, firstPath + "/versions", 1);

        var snapshots = new List<string>();
        for (var index = 0; index < 2; index++)
        {
            using var frozen = await owner.PostAsJsonAsync(TenantPath(tenantId) +
                "/scope-snapshots", new
                {
                    program_id = programId,
                    expected_program_revision = 1,
                    boundary_id = first.BoundaryId,
                    approved_boundary_version_id = first.VersionId,
                });
            Assert.Equal(HttpStatusCode.OK, frozen.StatusCode);
            snapshots.Add((await ReadAsync(frozen)).GetProperty("snapshot_id").GetString()!);
        }
        await WaitForPageCountAsync(owner, programPath + "/scope-snapshots", 2);
        foreach (var snapshotId in snapshots)
            _ = await WaitForAsync(owner, TenantPath(tenantId) + "/scope-snapshots/" +
                snapshotId + "/verification", static result =>
                    result.GetProperty("verified").GetBoolean());

        var second = await CreateBoundaryAsync(owner, tenantId, programPath,
            marker + " open draft");
        _ = await WaitForAsync(owner, TenantPath(tenantId) + "/boundaries/" +
            second.BoundaryId + "/drafts/" + second.VersionId +
            "/impact-preview?expected_revision=1", static result =>
                result.GetProperty("changes").GetArrayLength() > 0);
        await WaitForPageCountAsync(owner, programPath + "/boundaries", 2);
        return new TenantScope(tenantId, programId, [first.BoundaryId, second.BoundaryId],
            first.VersionId, second.VersionId, [reviewId, approvalId], snapshots);
    }

    static async Task GrantReviewerAsync(HttpClient owner, HttpClient reviewer,
        string reviewerEmail, MockTenantInvitationDelivery delivery, Uuid tenantId)
    {
        var tenantPath = TenantPath(tenantId);
        using var invitation = await owner.PostAsJsonAsync(tenantPath + "/invitations",
            new { email_address = reviewerEmail, affiliation = "client_personnel", administrator = false });
        Assert.Equal(HttpStatusCode.NoContent, invitation.StatusCode);
        string? token = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline &&
               !delivery.TryGetLatest(tenantId, reviewerEmail, out token))
            await Task.Delay(250);
        Assert.NotNull(token);
        var delivered = await WaitForAsync(owner, tenantPath +
            "/member-invitations?email_address=" + Uri.EscapeDataString(reviewerEmail),
            static page => page.GetProperty("items").EnumerateArray().Any(item =>
                item.GetProperty("delivery_status").GetString() == "delivered"));
        Assert.Equal(reviewerEmail, Assert.Single(delivered.GetProperty("items")
            .EnumerateArray()).GetProperty("email_address").GetString());
        using var accepted = await reviewer.PostAsJsonAsync(tenantPath +
            "/invitations/acceptance", new { email_address = reviewerEmail, token });
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        using var session = await reviewer.GetAsync("/auth/session");
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        var reviewerId = Uuid.Parse((await ReadAsync(session)).GetProperty("id").GetString()!,
            CultureInfo.InvariantCulture);
        var memberId = RbacIds.Member(tenantId, reviewerId);
        using var assigned = await owner.PostAsync(tenantPath + "/teams/" +
            BuiltInRbac.PowerUsersTeamId(tenantId) + "/members/" + memberId, null);
        Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);
    }

    static async Task<string> ReviewAsync(HttpClient owner, HttpClient reviewer,
        string boundaryPath, string versionId)
    {
        var draftPath = boundaryPath + "/drafts/" + versionId;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await reviewer.PostAsJsonAsync(draftPath + "/reviews", new
            {
                expected_revision = 1,
                outcome = "accept",
                rationale = "A separate tenant member reviewed this boundary.",
            });
            if (response.StatusCode == HttpStatusCode.NoContent)
                break;
            Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        var reviewed = await WaitForAsync(owner, boundaryPath, result =>
            result.GetProperty("latest_decision").ValueKind == JsonValueKind.Object &&
            result.GetProperty("latest_decision").GetProperty("outcome").GetString() == "accept");
        return reviewed.GetProperty("latest_decision").GetProperty("decision_id").GetString()!;
    }

    static async Task<Uuid> CreateTenantAsync(HttpClient owner, string name)
    {
        using var response = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name,
            slug = $"boundary-matrix-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Uuid.Parse((await ReadAsync(response)).GetProperty("tenant_id").GetString()!,
            CultureInfo.InvariantCulture);
    }

    static async Task<string> CreateProgramAsync(HttpClient owner, Uuid tenantId, string name)
    {
        var path = TenantPath(tenantId) + "/programs";
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(path, new
            {
                name,
                plan = new
                {
                    target_readiness_date = "2027-01-31",
                    target_type_i_as_of_date = "2027-03-31",
                    target_type_ii_start_date = "2027-04-01",
                    target_type_ii_end_date = "2028-03-31",
                    readiness_advisor = "Tenant read matrix",
                    audit_firm = (string?)null,
                },
            });
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var programId = (await ReadAsync(response)).GetProperty("program_id").GetString()!;
                _ = await WaitForAsync(owner, path + "/" + programId, result =>
                    result.GetProperty("revision").GetInt64() == 1);
                return programId;
            }
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("Program creation remained unavailable after tenant bootstrap.");
    }

    static async Task<BoundarySeed> CreateBoundaryAsync(HttpClient owner, Uuid tenantId,
        string programPath, string statement)
    {
        using var response = await owner.PostAsJsonAsync(programPath + "/boundaries", new
        {
            content = new
            {
                statement,
                engagement_stage = "readiness",
                trust_services_categories = SecurityCategory,
                entries = Array.Empty<object>(),
            },
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var registration = await ReadAsync(response);
        var boundaryId = registration.GetProperty("boundary_id").GetString()!;
        var versionId = registration.GetProperty("draft_version_id").GetString()!;
        _ = await WaitForAsync(owner, TenantPath(tenantId) + "/boundaries/" + boundaryId,
            result => result.GetProperty("draft").ValueKind == JsonValueKind.Object);
        return new BoundarySeed(boundaryId, versionId);
    }

    static async Task WaitForPageCountAsync(HttpClient client, string path, int count)
    {
        _ = await WaitForAsync(client, path, page =>
            page.GetProperty("items").GetArrayLength() == count);
    }

    static async Task<JsonElement> WaitForAsync(HttpClient client, string path,
        Func<JsonElement, bool> ready)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var result = await ReadAsync(response);
                if (ready(result))
                    return result;
            }
            else
                Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict,
                    await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("The read did not reach its expected state: " + path);
    }

    static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }

    static async Task<JsonElement> ReadHttpAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    static async Task<JsonElement> ReadMcpAsync(McpScenario mcp, string tool,
        Dictionary<string, object?> input)
    {
        var call = await mcp.When(tool, input).ExpectSuccess();
        return Assert.IsType<JsonElement>(call.StructuredJson).GetProperty("result");
    }

    static async Task<PageWalk> ReadHttpPagesAsync(HttpClient client, string path,
        int expectedCount)
    {
        var firstPath = path + "?limit=1";
        var first = await ReadHttpAsync(client, firstPath);
        var rows = new List<JsonElement>(Items(first));
        var firstCursor = first.GetProperty("next_cursor").GetString();
        if (expectedCount > 1)
            Assert.NotNull(firstCursor);
        var cursor = firstCursor;
        for (var pageIndex = 0; pageIndex < 4 && cursor is not null; pageIndex++)
        {
            var page = await ReadHttpAsync(client,
                firstPath + "&cursor=" + Uri.EscapeDataString(cursor));
            rows.AddRange(Items(page));
            cursor = page.GetProperty("next_cursor").GetString();
        }
        Assert.Null(cursor);
        Assert.Equal(expectedCount, rows.Count);
        return new PageWalk(rows.ToArray(), firstCursor);
    }

    static async Task<PageWalk> ReadMcpPagesAsync(McpScenario mcp, string tool,
        Dictionary<string, object?> input, int expectedCount)
    {
        var arguments = new Dictionary<string, object?>(input) { ["limit"] = 1 };
        var first = await ReadMcpAsync(mcp, tool, arguments);
        var rows = new List<JsonElement>(Items(first));
        var firstCursor = first.GetProperty("next_cursor").GetString();
        if (expectedCount > 1)
            Assert.NotNull(firstCursor);
        var cursor = firstCursor;
        for (var pageIndex = 0; pageIndex < 4 && cursor is not null; pageIndex++)
        {
            arguments["cursor"] = cursor;
            var page = await ReadMcpAsync(mcp, tool, arguments);
            rows.AddRange(Items(page));
            cursor = page.GetProperty("next_cursor").GetString();
        }
        Assert.Null(cursor);
        Assert.Equal(expectedCount, rows.Count);
        return new PageWalk(rows.ToArray(), firstCursor);
    }

    static void AssertRows(TenantScope scope, JsonElement[] rows, string idKey,
        IReadOnlyList<string> expectedIds)
    {
        Assert.Equal(expectedIds.Order(StringComparer.Ordinal),
            rows.Select(row => row.GetProperty(idKey).GetString()!).Order(StringComparer.Ordinal));
        Assert.All(rows, row =>
        {
            Assert.Equal(scope.TenantId.ToString(), row.GetProperty("tenant_id").GetString());
            if (row.TryGetProperty("program_id", out var programId))
                Assert.Equal(scope.ProgramId, programId.GetString());
            if (row.TryGetProperty("boundary_id", out var boundaryId))
                Assert.Contains(boundaryId.GetString()!, scope.BoundaryIds);
        });
    }

    static void AssertSnapshot(JsonElement result, TenantScope scope)
    {
        Assert.Equal(scope.TenantId.ToString(), result.GetProperty("tenant_id").GetString());
        Assert.Contains(result.GetProperty("snapshot_id").GetString()!, scope.SnapshotIds);
        Assert.Equal(scope.ProgramId, result.GetProperty("program_id").GetString());
        var manifest = result.GetProperty("manifest");
        Assert.Equal(scope.TenantId.ToString(), manifest.GetProperty("tenant_id").GetString());
        Assert.Equal(scope.ProgramId, manifest.GetProperty("program_id").GetString());
        Assert.Equal(scope.BoundaryIds[0], manifest.GetProperty("boundary_id").GetString());
        Assert.Equal(scope.ApprovedVersionId,
            manifest.GetProperty("approved_boundary_version_id").GetString());
    }

    static async Task AssertReadPairAsync(HttpClient owner, McpScenario mcp,
        string path, string tool, Dictionary<string, object?> input,
        Action<JsonElement> assertResult)
    {
        var http = await ReadHttpAsync(owner, path);
        var mcpResult = await ReadMcpAsync(mcp, tool, input);
        Assert.Equal(input["tenant_id"]?.ToString(), http.GetProperty("tenant_id").GetString());
        Assert.Equal(input["tenant_id"]?.ToString(), mcpResult.GetProperty("tenant_id").GetString());
        assertResult(http);
        assertResult(mcpResult);
    }

    static JsonElement[] Items(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().ToArray();

    static string TenantPath(Uuid tenantId) => "/api/v1/tenants/" + tenantId;

    static IReadOnlyList<ReadCase> ReadCases(TenantScope source, Uuid requestedTenant)
    {
        var tenantPath = TenantPath(requestedTenant);
        var programPath = tenantPath + "/programs/" + source.ProgramId;
        var boundaryPath = tenantPath + "/boundaries/" + source.BoundaryIds[0];
        var draftPath = tenantPath + "/boundaries/" + source.BoundaryIds[1] +
            "/drafts/" + source.DraftVersionId;
        var snapshotPath = tenantPath + "/scope-snapshots/" + source.SnapshotIds[0];
        Dictionary<string, object?> Args() => new() { ["tenant_id"] = requestedTenant.ToString() };
        return
        [
            new(programPath + "/boundaries", "bdgrz.boundary.program.list",
                new Dictionary<string, object?>(Args()) { ["program_id"] = source.ProgramId }),
            new(boundaryPath, "bdgrz.boundary.get",
                new Dictionary<string, object?>(Args()) { ["boundary_id"] = source.BoundaryIds[0] }),
            new(boundaryPath + "/versions", "bdgrz.boundary.versions.list",
                new Dictionary<string, object?>(Args()) { ["boundary_id"] = source.BoundaryIds[0] }),
            new(boundaryPath + "/versions/" + source.ApprovedVersionId,
                "bdgrz.boundary.version.get", new Dictionary<string, object?>(Args())
                {
                    ["boundary_id"] = source.BoundaryIds[0],
                    ["version_id"] = source.ApprovedVersionId,
                }),
            new(boundaryPath + "/effective-version?effective_on=2027-01-15",
                "bdgrz.boundary.version.effective.get", new Dictionary<string, object?>(Args())
                {
                    ["boundary_id"] = source.BoundaryIds[0],
                    ["effective_on"] = "2027-01-15",
                }),
            new(boundaryPath + "/decisions", "bdgrz.boundary.decisions.list",
                new Dictionary<string, object?>(Args()) { ["boundary_id"] = source.BoundaryIds[0] }),
            new(boundaryPath + "/decisions/" + source.DecisionIds[0],
                "bdgrz.boundary.decision.get", new Dictionary<string, object?>(Args())
                {
                    ["boundary_id"] = source.BoundaryIds[0],
                    ["decision_id"] = source.DecisionIds[0],
                }),
            new(draftPath + "/impact-preview?expected_revision=1",
                "bdgrz.boundary.impact.preview", new Dictionary<string, object?>(Args())
                {
                    ["boundary_id"] = source.BoundaryIds[1],
                    ["draft_version_id"] = source.DraftVersionId,
                    ["expected_revision"] = 1,
                }),
            new(programPath + "/scope-snapshots", "bdgrz.snapshot.program.list",
                new Dictionary<string, object?>(Args()) { ["program_id"] = source.ProgramId }),
            new(snapshotPath, "bdgrz.snapshot.get",
                new Dictionary<string, object?>(Args()) { ["snapshot_id"] = source.SnapshotIds[0] }),
            new(snapshotPath + "/verification", "bdgrz.snapshot.program_scope.verify",
                new Dictionary<string, object?>(Args()) { ["snapshot_id"] = source.SnapshotIds[0] }),
            new(snapshotPath + "/manifest-regeneration",
                "bdgrz.snapshot.program_scope.manifest_regenerate",
                new Dictionary<string, object?>(Args()) { ["snapshot_id"] = source.SnapshotIds[0] }),
        ];
    }

    static async Task AssertDeniedCasesAsync(HttpClient client, McpScenario mcp,
        IReadOnlyList<ReadCase> cases)
    {
        foreach (var read in cases)
        {
            using var response = await client.GetAsync(read.HttpPath);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            _ = await mcp.When(read.McpTool, read.McpInput).ExpectFailure("NotFound");
        }
    }

    static async Task AssertTransplantedCursorsAsync(HttpClient owner, McpScenario mcp,
        TenantScope scope, IReadOnlyDictionary<string, CursorPair> sourceCursors)
    {
        var tenantPath = TenantPath(scope.TenantId);
        var probes = new[]
        {
            new CursorProbe("boundaries", tenantPath + "/programs/" + scope.ProgramId +
                "/boundaries?limit=1", "bdgrz.boundary.program.list",
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = scope.TenantId.ToString(),
                    ["program_id"] = scope.ProgramId,
                    ["limit"] = 1,
                }, "boundary_id", scope.BoundaryIds),
            new CursorProbe("decisions", tenantPath + "/boundaries/" + scope.BoundaryIds[0] +
                "/decisions?limit=1", "bdgrz.boundary.decisions.list",
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = scope.TenantId.ToString(),
                    ["boundary_id"] = scope.BoundaryIds[0],
                    ["limit"] = 1,
                }, "decision_id", scope.DecisionIds),
            new CursorProbe("snapshots", tenantPath + "/programs/" + scope.ProgramId +
                "/scope-snapshots?limit=1", "bdgrz.snapshot.program.list",
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = scope.TenantId.ToString(),
                    ["program_id"] = scope.ProgramId,
                    ["limit"] = 1,
                }, "snapshot_id", scope.SnapshotIds),
        };
        foreach (var probe in probes)
        {
            var cursors = sourceCursors[probe.Name];
            await AssertTransplantedHttpAsync(owner, probe, cursors.Http, scope);
            await AssertTransplantedMcpAsync(mcp, probe, cursors.Mcp, scope);
        }
    }

    static async Task AssertTransplantedHttpAsync(HttpClient client, CursorProbe probe,
        string cursor, TenantScope scope)
    {
        for (var pageIndex = 0; pageIndex < 4; pageIndex++)
        {
            using var response = await client.GetAsync(probe.HttpPath + "&cursor=" +
                Uri.EscapeDataString(cursor));
            if (response.StatusCode == HttpStatusCode.BadRequest)
                return;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var page = await ReadAsync(response);
            AssertForeignRows(page, probe, scope);
            var nextCursor = page.GetProperty("next_cursor").GetString();
            if (nextCursor is null)
                return;
            cursor = nextCursor;
        }
        Assert.Fail("The transplanted HTTP cursor did not terminate.");
    }

    static async Task AssertTransplantedMcpAsync(McpScenario mcp, CursorProbe probe,
        string cursor, TenantScope scope)
    {
        var arguments = new Dictionary<string, object?>(probe.McpInput);
        for (var pageIndex = 0; pageIndex < 4; pageIndex++)
        {
            arguments["cursor"] = cursor;
            var call = await mcp.When(probe.McpTool, arguments);
            var structured = Assert.IsType<JsonElement>(call.StructuredJson);
            if (call.IsError)
            {
                Assert.Equal("Validation", structured.GetProperty("kind").GetString());
                return;
            }
            var page = structured.GetProperty("result");
            AssertForeignRows(page, probe, scope);
            var nextCursor = page.GetProperty("next_cursor").GetString();
            if (nextCursor is null)
                return;
            cursor = nextCursor;
        }
        Assert.Fail("The transplanted MCP cursor did not terminate.");
    }

    static void AssertForeignRows(JsonElement page, CursorProbe probe, TenantScope scope)
    {
        Assert.All(Items(page), row =>
        {
            Assert.Equal(scope.TenantId.ToString(), row.GetProperty("tenant_id").GetString());
            Assert.Contains(row.GetProperty(probe.IdKey).GetString()!, probe.AllowedIds);
            if (row.TryGetProperty("program_id", out var programId))
                Assert.Equal(scope.ProgramId, programId.GetString());
            if (row.TryGetProperty("boundary_id", out var boundaryId))
                Assert.Contains(boundaryId.GetString()!, scope.BoundaryIds);
        });
    }

    sealed record TenantScope(Uuid TenantId, string ProgramId, IReadOnlyList<string> BoundaryIds,
        string ApprovedVersionId, string DraftVersionId, IReadOnlyList<string> DecisionIds,
        IReadOnlyList<string> SnapshotIds);

    sealed record BoundarySeed(string BoundaryId, string VersionId);

    sealed record PageWalk(JsonElement[] Rows, string? FirstCursor);

    sealed record CursorPair(string Http, string Mcp);

    sealed record CursorProbe(string Name, string HttpPath, string McpTool,
        Dictionary<string, object?> McpInput, string IdKey, IReadOnlyList<string> AllowedIds);

    sealed record ReadCase(string HttpPath, string McpTool,
        Dictionary<string, object?> McpInput);
}
