using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ProgramReadLeakMatrixE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    static readonly string[] SecurityCategory = ["security"];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ShouldScopeProgramAndServicePagesGivenTwoTenants(bool splitHosts) =>
        RunWithHostsAsync(splitHosts, async (owner, outsider) =>
        {
            // Arrange
            await TenantInvitationE2ETests.LoginAsync(owner,
                $"program-matrix-owner-{Guid.NewGuid():N}@example.com");
            await TenantInvitationE2ETests.LoginAsync(outsider,
                $"program-matrix-outsider-{Guid.NewGuid():N}@example.com");
            var tenantA = await CreateTenantAsync(owner, "Program matrix A");
            var tenantB = await CreateTenantAsync(owner, "Program matrix B");
            var scopeA = await SeedAsync(owner, tenantA, "A");
            var scopeB = await SeedAsync(owner, tenantB, "B");

            // Act: page every current and historical read through HTTP and MCP.
            await using var mcp = await McpScenario.ConnectAsync(owner,
                new Uri(owner.BaseAddress!, "/mcp"));
            var tenantACursors = new Dictionary<string, CursorPair>();
            foreach (var scope in new[] { scopeA, scopeB })
            {
                var tenantPath = TenantPath(scope.TenantId);
                var programPath = tenantPath + "/programs/" + scope.ProgramIds[0];
                var servicePath = tenantPath + "/client-services/" + scope.ServiceIds[0];
                var programInput = new Dictionary<string, object?>
                {
                    ["tenant_id"] = scope.TenantId.ToString(),
                };
                var programHistoryInput = new Dictionary<string, object?>(programInput)
                {
                    ["program_id"] = scope.ProgramIds[0],
                    ["minimum_program_revision"] = 2,
                };
                var serviceInput = new Dictionary<string, object?>(programInput);
                var programServicesInput = new Dictionary<string, object?>(programInput)
                {
                    ["program_id"] = scope.ProgramIds[0],
                };
                var serviceHistoryInput = new Dictionary<string, object?>(programInput)
                {
                    ["service_id"] = scope.ServiceIds[0],
                    ["minimum_service_revision"] = 2,
                };

                var httpPrograms = await ReadHttpPagesAsync(owner, tenantPath + "/programs");
                var mcpPrograms = await ReadMcpPagesAsync(mcp, "bdgrz.program.list",
                    programInput);
                AssertCurrentRows(scope, httpPrograms.Entries, "program_id", scope.ProgramIds);
                AssertCurrentRows(scope, mcpPrograms.Entries, "program_id", scope.ProgramIds);

                var httpProgramHistory = await ReadHttpPagesAsync(owner,
                    programPath + "/revisions", "minimum_program_revision=2");
                var mcpProgramHistory = await ReadMcpPagesAsync(mcp,
                    "bdgrz.program.revisions.list", programHistoryInput);
                AssertHistory(httpProgramHistory.Entries, "program_id", scope.ProgramIds[0],
                    scope.Marker);
                AssertHistory(mcpProgramHistory.Entries, "program_id", scope.ProgramIds[0],
                    scope.Marker);

                var httpSetup = await ReadSetupHttpPagesAsync(owner,
                    programPath + "/setup-work");
                var mcpSetup = await ReadSetupMcpPagesAsync(mcp,
                    scope.TenantId, scope.ProgramIds[0]);
                AssertSetup(scope, httpSetup.Entries);
                AssertSetup(scope, mcpSetup.Entries);

                var httpServices = await ReadHttpPagesAsync(owner,
                    tenantPath + "/client-services");
                var mcpServices = await ReadMcpPagesAsync(mcp,
                    "bdgrz.client-service.list", serviceInput);
                AssertCurrentRows(scope, httpServices.Entries, "service_id", scope.ServiceIds);
                AssertCurrentRows(scope, mcpServices.Entries, "service_id", scope.ServiceIds);

                var httpProgramServices = await ReadHttpPagesAsync(owner,
                    programPath + "/client-services");
                var mcpProgramServices = await ReadMcpPagesAsync(mcp,
                    "bdgrz.client-service.program.list", programServicesInput);
                AssertCurrentRows(scope, httpProgramServices.Entries, "service_id",
                    scope.ServiceIds, "program_id", scope.ProgramIds[0]);
                AssertCurrentRows(scope, mcpProgramServices.Entries, "service_id",
                    scope.ServiceIds, "program_id", scope.ProgramIds[0]);

                var httpServiceHistory = await ReadHttpPagesAsync(owner,
                    servicePath + "/revisions", "minimum_service_revision=2");
                var mcpServiceHistory = await ReadMcpPagesAsync(mcp,
                    "bdgrz.client-service.revisions.list", serviceHistoryInput);
                AssertHistory(httpServiceHistory.Entries, "service_id", scope.ServiceIds[0],
                    scope.Marker);
                AssertHistory(mcpServiceHistory.Entries, "service_id", scope.ServiceIds[0],
                    scope.Marker);

                if (scope.TenantId == tenantA)
                {
                    tenantACursors.Add("programs", new CursorPair(httpPrograms.FirstCursor,
                        mcpPrograms.FirstCursor));
                    tenantACursors.Add("program_history", new CursorPair(httpProgramHistory.FirstCursor,
                        mcpProgramHistory.FirstCursor));
                    tenantACursors.Add("setup", new CursorPair(httpSetup.FirstCursor,
                        mcpSetup.FirstCursor));
                    tenantACursors.Add("services", new CursorPair(httpServices.FirstCursor,
                        mcpServices.FirstCursor));
                    tenantACursors.Add("program_services", new CursorPair(
                        httpProgramServices.FirstCursor, mcpProgramServices.FirstCursor));
                    tenantACursors.Add("service_history", new CursorPair(
                        httpServiceHistory.FirstCursor, mcpServiceHistory.FirstCursor));
                }
            }

            // A cursor issued in tenant A is invalid in tenant B (ADR 0009).
            var tenantBPath = TenantPath(tenantB);
            var cursorProbes = new[]
            {
                new CursorProbe("programs", tenantBPath + "/programs?limit=1",
                    "bdgrz.program.list", new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenantB.ToString(), ["limit"] = 1,
                    }),
                new CursorProbe("program_history", tenantBPath + "/programs/" +
                    scopeB.ProgramIds[0] + "/revisions?limit=1&minimum_program_revision=2",
                    "bdgrz.program.revisions.list", new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenantB.ToString(), ["program_id"] = scopeB.ProgramIds[0],
                        ["limit"] = 1, ["minimum_program_revision"] = 2,
                    }),
                new CursorProbe("services", tenantBPath + "/client-services?limit=1",
                    "bdgrz.client-service.list", new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenantB.ToString(), ["limit"] = 1,
                    }),
                new CursorProbe("program_services", tenantBPath + "/programs/" +
                    scopeB.ProgramIds[0] + "/client-services?limit=1",
                    "bdgrz.client-service.program.list", new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenantB.ToString(), ["program_id"] = scopeB.ProgramIds[0],
                        ["limit"] = 1,
                    }),
                new CursorProbe("service_history", tenantBPath + "/client-services/" +
                    scopeB.ServiceIds[0] + "/revisions?limit=1&minimum_service_revision=2",
                    "bdgrz.client-service.revisions.list", new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenantB.ToString(), ["service_id"] = scopeB.ServiceIds[0],
                        ["limit"] = 1, ["minimum_service_revision"] = 2,
                    }),
            };
            foreach (var probe in cursorProbes)
            {
                var cursors = tenantACursors[probe.Name];
                await AssertTransplantedHttpCursorAsync(owner, probe.HttpPath, cursors.Http);
                await AssertTransplantedMcpCursorAsync(mcp, probe.McpTool, probe.McpInput,
                    cursors.Mcp);
            }
            await AssertTransplantedSetupCursorAsync(owner, mcp, scopeB,
                tenantACursors["setup"]);

            // Assert: a member of both tenants cannot use one tenant's record IDs in the other.
            foreach (var path in new[]
                     {
                         tenantBPath + "/programs/" + scopeA.ProgramIds[0] + "/revisions",
                         tenantBPath + "/programs/" + scopeA.ProgramIds[0] + "/setup-work",
                         tenantBPath + "/programs/" + scopeA.ProgramIds[0] + "/client-services",
                         tenantBPath + "/client-services/" + scopeA.ServiceIds[0] + "/revisions",
                     })
            {
                using var foreign = await owner.GetAsync(path);
                Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
            }
            foreach (var (tool, idKey, id) in new[]
                     {
                         ("bdgrz.program.revisions.list", "program_id", scopeA.ProgramIds[0]),
                         ("bdgrz.program.setup-work.get", "program_id", scopeA.ProgramIds[0]),
                         ("bdgrz.client-service.program.list", "program_id", scopeA.ProgramIds[0]),
                         ("bdgrz.client-service.revisions.list", "service_id", scopeA.ServiceIds[0]),
                     })
            {
                _ = await mcp.When(tool, new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantB.ToString(),
                    [idKey] = id,
                }).ExpectFailure("NotFound");
            }

            // An unrelated authenticated user cannot discover any of these read families.
            var tenantAPath = TenantPath(tenantA);
            foreach (var path in new[]
                     {
                         tenantAPath + "/programs",
                         tenantAPath + "/programs/" + scopeA.ProgramIds[0] + "/revisions",
                         tenantAPath + "/programs/" + scopeA.ProgramIds[0] + "/setup-work",
                         tenantAPath + "/client-services",
                         tenantAPath + "/programs/" + scopeA.ProgramIds[0] + "/client-services",
                         tenantAPath + "/client-services/" + scopeA.ServiceIds[0] + "/revisions",
                     })
            {
                using var denied = await outsider.GetAsync(path);
                Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            }
            await using var outsiderMcp = await McpScenario.ConnectAsync(outsider,
                new Uri(outsider.BaseAddress!, "/mcp"));
            foreach (var (tool, idKey, id) in new[]
                     {
                         ("bdgrz.program.list", "", ""),
                         ("bdgrz.program.revisions.list", "program_id", scopeA.ProgramIds[0]),
                         ("bdgrz.program.setup-work.get", "program_id", scopeA.ProgramIds[0]),
                         ("bdgrz.client-service.list", "", ""),
                         ("bdgrz.client-service.program.list", "program_id", scopeA.ProgramIds[0]),
                         ("bdgrz.client-service.revisions.list", "service_id", scopeA.ServiceIds[0]),
                     })
            {
                var input = new Dictionary<string, object?> { ["tenant_id"] = tenantA.ToString() };
                if (idKey.Length != 0)
                    input[idKey] = id;
                _ = await outsiderMcp.When(tool, input).ExpectFailure("NotFound");
            }
        });

    async Task RunWithHostsAsync(bool splitHosts, Func<HttpClient, HttpClient, Task> exercise)
    {
        var applicationName = $"compliance-program-read-matrix-{Guid.NewGuid():N}";
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

    static async Task<TenantScope> SeedAsync(HttpClient owner, Uuid tenantId, string marker)
    {
        var programsPath = TenantPath(tenantId) + "/programs";
        var firstProgram = await CreateProgramAsync(owner, programsPath, marker + " Program 1");
        var secondProgram = await CreateProgramAsync(owner, programsPath, marker + " Program 2");
        await WaitForPageCountAsync(owner, programsPath, 2);
        using var revisedProgram = await owner.PutAsJsonAsync(programsPath + "/" + firstProgram,
            new
            {
                expected_revision = 1,
                name = marker + " Program 1 revised",
                plan = Plan(),
            });
        Assert.Equal(HttpStatusCode.NoContent, revisedProgram.StatusCode);
        await WaitForPageCountAsync(owner, programsPath + "/" + firstProgram +
            "/revisions?minimum_program_revision=2", 2);

        var servicesPath = TenantPath(tenantId) + "/client-services";
        var createServicesPath = programsPath + "/" + firstProgram + "/client-services";
        var firstService = await CreateServiceAsync(owner, createServicesPath,
            marker + " Service 1");
        var secondService = await CreateServiceAsync(owner, createServicesPath,
            marker + " Service 2");
        await WaitForPageCountAsync(owner, servicesPath, 2);
        await WaitForPageCountAsync(owner, createServicesPath, 2);
        using (var revised = await owner.PutAsJsonAsync(servicesPath + "/" + firstService,
                   new
                   {
                       expected_revision = 1,
                       name = marker + " Service 1 revised",
                       purpose = "Updated tenant read matrix",
                       owner_reference = "Operations",
                   }))
            Assert.Equal(HttpStatusCode.NoContent, revised.StatusCode);
        await WaitForPageCountAsync(owner, servicesPath + "/" + firstService +
            "/revisions?minimum_service_revision=2", 2);

        var boundariesPath = programsPath + "/" + firstProgram + "/boundaries";
        var firstBoundary = await CreateBoundaryAsync(owner, boundariesPath,
            marker + " boundary 1");
        var secondBoundary = await CreateBoundaryAsync(owner, boundariesPath,
            marker + " boundary 2");
        await WaitForPageCountAsync(owner, boundariesPath, 2);
        return new TenantScope(marker, tenantId, [firstProgram, secondProgram],
            [firstService, secondService], [firstBoundary, secondBoundary]);
    }

    static async Task<Uuid> CreateTenantAsync(HttpClient owner, string name)
    {
        using var response = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name,
            slug = $"program-matrix-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Uuid.Parse((await ReadJsonAsync(response)).GetProperty("tenant_id").GetString()!,
            CultureInfo.InvariantCulture);
    }

    static async Task<string> CreateProgramAsync(HttpClient owner, string path, string name)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(path, new { name, plan = Plan() });
            if (response.StatusCode == HttpStatusCode.OK)
                return (await ReadJsonAsync(response)).GetProperty("program_id").GetString()!;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("Program creation remained unauthorized after tenant bootstrap.");
    }

    static async Task<string> CreateServiceAsync(HttpClient owner, string path, string name)
    {
        using var response = await owner.PostAsJsonAsync(path, new
        {
            name,
            purpose = "Tenant read matrix",
            owner_reference = "Operations",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("service_id").GetString()!;
    }

    static async Task<string> CreateBoundaryAsync(HttpClient owner, string path, string statement)
    {
        using var response = await owner.PostAsJsonAsync(path, new
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
        return (await ReadJsonAsync(response)).GetProperty("boundary_id").GetString()!;
    }

    static object Plan() => new
    {
        target_readiness_date = "2027-01-31",
        target_type_i_as_of_date = "2027-03-31",
        target_type_ii_start_date = "2027-04-01",
        target_type_ii_end_date = "2028-03-31",
        readiness_advisor = "Tenant read matrix",
        audit_firm = (string?)null,
    };

    static async Task WaitForPageCountAsync(HttpClient client, string path, int count)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK &&
                (await ReadJsonAsync(response)).GetProperty("items").GetArrayLength() == count)
                return;
            await Task.Delay(250);
        }
        throw new TimeoutException("The projection did not reach the expected page size: " + path);
    }

    static async Task<PageWalk> ReadHttpPagesAsync(HttpClient client, string path,
        string? query = null)
    {
        var firstPath = path + "?limit=1" + (query is null ? "" : "&" + query);
        var first = await ReadHttpAsync(client, firstPath);
        var firstCursor = first.GetProperty("next_cursor").GetString();
        Assert.NotNull(firstCursor);
        var entries = new List<JsonElement> { Assert.Single(Items(first)) };
        var cursor = firstCursor;
        for (var pageIndex = 0; pageIndex < 3; pageIndex++)
        {
            var page = await ReadHttpAsync(client,
                firstPath + "&cursor=" + Uri.EscapeDataString(cursor));
            entries.AddRange(Items(page));
            var nextCursor = page.GetProperty("next_cursor").GetString();
            if (nextCursor is null)
            {
                Assert.Equal(2, entries.Count);
                return new PageWalk(entries.ToArray(), firstCursor);
            }
            cursor = nextCursor;
        }
        throw new Xunit.Sdk.XunitException("The HTTP cursor did not terminate.");
    }

    static async Task<PageWalk> ReadMcpPagesAsync(McpScenario mcp, string tool,
        Dictionary<string, object?> input)
    {
        var arguments = new Dictionary<string, object?>(input) { ["limit"] = 1 };
        var first = await ReadMcpAsync(mcp, tool, arguments);
        var firstCursor = first.GetProperty("next_cursor").GetString();
        Assert.NotNull(firstCursor);
        var entries = new List<JsonElement> { Assert.Single(Items(first)) };
        var cursor = firstCursor;
        for (var pageIndex = 0; pageIndex < 3; pageIndex++)
        {
            arguments["cursor"] = cursor;
            var page = await ReadMcpAsync(mcp, tool, arguments);
            entries.AddRange(Items(page));
            var nextCursor = page.GetProperty("next_cursor").GetString();
            if (nextCursor is null)
            {
                Assert.Equal(2, entries.Count);
                return new PageWalk(entries.ToArray(), firstCursor);
            }
            cursor = nextCursor;
        }
        throw new Xunit.Sdk.XunitException("The MCP cursor did not terminate.");
    }

    static async Task<PageWalk> ReadSetupHttpPagesAsync(HttpClient client, string path)
    {
        var first = await WaitForSetupHttpAsync(client, path + "?boundary_limit=1",
            requireNextCursor: true);
        var firstCursor = first.GetProperty("next_boundary_cursor").GetString();
        Assert.NotNull(firstCursor);
        var pages = new List<JsonElement> { first };
        var cursor = firstCursor;
        for (var pageIndex = 0; pageIndex < 3; pageIndex++)
        {
            var page = await WaitForSetupHttpAsync(client,
                path + "?boundary_limit=1&boundary_cursor=" +
                Uri.EscapeDataString(cursor));
            pages.Add(page);
            var nextCursor = page.GetProperty("next_boundary_cursor").GetString();
            if (nextCursor is null)
                return new PageWalk(pages.ToArray(), firstCursor);
            cursor = nextCursor;
        }
        throw new Xunit.Sdk.XunitException("The HTTP setup cursor did not terminate.");
    }

    static async Task<PageWalk> ReadSetupMcpPagesAsync(McpScenario mcp,
        Uuid tenantId, string programId)
    {
        var input = new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId.ToString(),
            ["program_id"] = programId,
            ["boundary_limit"] = 1,
        };
        var first = await WaitForSetupMcpAsync(mcp, input, requireNextCursor: true);
        var firstCursor = first.GetProperty("next_boundary_cursor").GetString();
        Assert.NotNull(firstCursor);
        var pages = new List<JsonElement> { first };
        var cursor = firstCursor;
        for (var pageIndex = 0; pageIndex < 3; pageIndex++)
        {
            input["boundary_cursor"] = cursor;
            var page = await WaitForSetupMcpAsync(mcp, input);
            pages.Add(page);
            var nextCursor = page.GetProperty("next_boundary_cursor").GetString();
            if (nextCursor is null)
                return new PageWalk(pages.ToArray(), firstCursor);
            cursor = nextCursor;
        }
        throw new Xunit.Sdk.XunitException("The MCP setup cursor did not terminate.");
    }

    static async Task<JsonElement> WaitForSetupHttpAsync(HttpClient client, string path,
        bool requireNextCursor = false)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var page = await ReadJsonAsync(response);
                if (!requireNextCursor || page.GetProperty("next_boundary_cursor").GetString() is not null)
                    return page;
                await Task.Delay(250);
                continue;
            }
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("true", response.Headers.GetValues("Portia-Transient").Single());
            await Task.Delay(250);
        }
        throw new TimeoutException("The setup-work projection did not catch up: " + path);
    }

    static async Task<JsonElement> WaitForSetupMcpAsync(McpScenario mcp,
        Dictionary<string, object?> input, bool requireNextCursor = false)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var call = await mcp.When("bdgrz.program.setup-work.get", input);
            var structured = Assert.IsType<JsonElement>(call.StructuredJson);
            if (!call.IsError)
            {
                var page = structured.GetProperty("result");
                if (!requireNextCursor || page.GetProperty("next_boundary_cursor").GetString() is not null)
                    return page;
                await Task.Delay(250);
                continue;
            }
            Assert.Equal("Conflict", structured.GetProperty("kind").GetString());
            Assert.True(structured.GetProperty("isTransient").GetBoolean());
            await Task.Delay(250);
        }
        throw new TimeoutException("The MCP setup-work projection did not catch up.");
    }

    static async Task AssertTransplantedHttpCursorAsync(HttpClient client, string path,
        string cursor)
    {
        using var response = await client.GetAsync(path + "&cursor=" +
            Uri.EscapeDataString(cursor));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    static async Task AssertTransplantedMcpCursorAsync(McpScenario mcp, string tool,
        Dictionary<string, object?> input, string cursor)
    {
        var arguments = new Dictionary<string, object?>(input)
        {
            ["cursor"] = cursor,
        };
        _ = await mcp.When(tool, arguments).ExpectFailure("Validation");
    }

    static async Task AssertTransplantedSetupCursorAsync(HttpClient client, McpScenario mcp,
        TenantScope scope, CursorPair cursors)
    {
        var path = TenantPath(scope.TenantId) + "/programs/" + scope.ProgramIds[0] +
            "/setup-work?boundary_limit=1&boundary_cursor=";
        using var response = await client.GetAsync(path + Uri.EscapeDataString(cursors.Http));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _ = await mcp.When("bdgrz.program.setup-work.get",
            new Dictionary<string, object?>
            {
                ["tenant_id"] = scope.TenantId.ToString(),
                ["program_id"] = scope.ProgramIds[0],
                ["boundary_limit"] = 1,
                ["boundary_cursor"] = cursors.Mcp,
            }).ExpectFailure("Validation");
    }

    static void AssertTenantSetup(JsonElement page, TenantScope scope)
    {
        Assert.Equal(scope.TenantId.ToString(), page.GetProperty("tenant_id").GetString());
        Assert.Equal(scope.ProgramIds[0], page.GetProperty("program_id").GetString());
        Assert.All(Items(page), item =>
        {
            Assert.Equal("boundary", item.GetProperty("source_type").GetString());
            Assert.Contains(item.GetProperty("source_id").GetString()!, scope.BoundaryIds);
        });
    }

    static void AssertCurrentRows(TenantScope scope, JsonElement[] rows, string idKey,
        IReadOnlyList<string> expectedIds, string? parentKey = null, string? parentId = null)
    {
        Assert.Equal(expectedIds.Order(StringComparer.Ordinal),
            rows.Select(row => row.GetProperty(idKey).GetString()!).Order(StringComparer.Ordinal));
        Assert.All(rows, row =>
        {
            Assert.Equal(scope.TenantId.ToString(), row.GetProperty("tenant_id").GetString());
            if (parentKey is not null)
                Assert.Equal(parentId, row.GetProperty(parentKey).GetString());
            var index = Array.IndexOf(expectedIds.ToArray(), row.GetProperty(idKey).GetString());
            Assert.InRange(index, 0, 1);
            var kind = idKey == "program_id" ? "Program" : "Service";
            Assert.Equal($"{scope.Marker} {kind} {index + 1}" +
                (index == 0 ? " revised" : string.Empty),
                row.GetProperty("name").GetString());
            Assert.Equal(index == 0 ? 2 : 1, row.GetProperty("revision").GetInt64());
            if (kind == "Service")
                Assert.Equal(index == 0 ? "Updated tenant read matrix" : "Tenant read matrix",
                    row.GetProperty("purpose").GetString());
        });
    }

    static void AssertHistory(JsonElement[] rows, string parentKey, string parentId,
        string marker)
    {
        Assert.Equal([1L, 2L], rows.Select(row => row.GetProperty("revision").GetInt64())
            .Order());
        Assert.All(rows, row =>
        {
            Assert.Equal(parentId, row.GetProperty(parentKey).GetString());
            var revision = row.GetProperty("revision").GetInt64();
            var kind = parentKey == "program_id" ? "Program" : "Service";
            Assert.Equal($"{marker} {kind} 1" +
                (revision == 2 ? " revised" : string.Empty),
                row.GetProperty("name").GetString());
            if (kind == "Service")
                Assert.Equal(revision == 2 ? "Updated tenant read matrix" :
                    "Tenant read matrix", row.GetProperty("purpose").GetString());
        });
    }

    static void AssertSetup(TenantScope scope, JsonElement[] pages)
    {
        Assert.Equal(2, pages.Count(page => Items(page).Length != 0));
        Assert.All(pages, page =>
        {
            Assert.Equal(scope.TenantId.ToString(), page.GetProperty("tenant_id").GetString());
            Assert.Equal(scope.ProgramIds[0], page.GetProperty("program_id").GetString());
            Assert.Equal(2, page.GetProperty("program_revision").GetInt64());
        });
        var items = pages.SelectMany(Items).ToArray();
        Assert.NotEmpty(items);
        Assert.All(items, item =>
        {
            Assert.Equal("boundary", item.GetProperty("source_type").GetString());
            Assert.Contains(item.GetProperty("source_id").GetString(), scope.BoundaryIds);
        });
        var boundaryIds = items
            .Select(item => item.GetProperty("source_id").GetString()!)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        Assert.Equal(scope.BoundaryIds.Order(StringComparer.Ordinal), boundaryIds);
    }

    static JsonElement[] Items(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().ToArray();

    static async Task<JsonElement> ReadHttpAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    static async Task<JsonElement> ReadMcpAsync(McpScenario mcp, string tool,
        Dictionary<string, object?> input)
    {
        var call = await mcp.When(tool, input).ExpectSuccess();
        return Assert.IsType<JsonElement>(call.StructuredJson).GetProperty("result");
    }

    static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }

    static string TenantPath(Uuid tenantId) => "/api/v1/tenants/" + tenantId;

    sealed record TenantScope(string Marker, Uuid TenantId, IReadOnlyList<string> ProgramIds,
        IReadOnlyList<string> ServiceIds, IReadOnlyList<string> BoundaryIds);

    sealed record PageWalk(JsonElement[] Entries, string FirstCursor);

    sealed record CursorPair(string Http, string Mcp);

    sealed record CursorProbe(string Name, string HttpPath, string McpTool,
        Dictionary<string, object?> McpInput);
}
