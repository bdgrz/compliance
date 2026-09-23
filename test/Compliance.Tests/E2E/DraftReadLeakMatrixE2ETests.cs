using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     EN-01: current control, commitment, and risk drafts and their history stay in
///     the requested tenant and program over HTTP and MCP in both host modes.
/// </summary>
[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class DraftReadLeakMatrixE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldScopeDraftAndHistoryReadsGivenTwoPopulatedTenants(bool splitHosts)
    {
        // Arrange
        var applicationName = $"compliance-draft-read-{Guid.NewGuid():N}";
        using var worker = splitHosts ? BuildWorker(applicationName) : null;
        if (worker is not null)
            await worker.StartAsync();
        try
        {
            await using var factory = E2EAppFactory.Create(broker, applicationName);
            var previousMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
            HttpClient owner;
            try
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE",
                    splitHosts ? "api" : "standalone");
                owner = factory.CreateClient();
            }
            finally
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", previousMode);
            }
            using (owner)
            using (var outsider = factory.CreateClient())
            {
                await TenantInvitationE2ETests.LoginAsync(owner,
                    $"draft-matrix-owner-{Guid.NewGuid():N}@example.com");
                await TenantInvitationE2ETests.LoginAsync(outsider,
                    $"draft-matrix-outsider-{Guid.NewGuid():N}@example.com");
                var first = await SeedAsync(owner, "A");
                var second = await SeedAsync(owner, "B");

                // Act
                // Assert: exhaust every populated list, including a possible empty terminal page.
                foreach (var seed in new[] { first, second })
                {
                    foreach (var spec in ListSpecs(seed))
                        await AssertHttpPagesAsync(owner, spec);
                    foreach (var spec in ExactSpecs(seed))
                        AssertExact(await ReadHttpAsync(owner, spec.Path), spec);
                }
                foreach (var spec in ListSpecs(first))
                {
                    var foreign = ListSpecs(second).Single(other => other.Tool == spec.Tool);
                    await AssertHttpCursorTransplantAsync(owner, spec, foreign);
                }

                await using var mcp = await McpScenario.ConnectAsync(owner,
                    new Uri(owner.BaseAddress!, "/mcp"));
                foreach (var seed in new[] { first, second })
                {
                    foreach (var spec in ListSpecs(seed))
                        await AssertMcpPagesAsync(mcp, spec);
                    foreach (var spec in ExactSpecs(seed))
                        AssertExact(await ReadToolAsync(mcp, spec.Tool, spec.Args), spec);
                }
                foreach (var spec in ListSpecs(first))
                {
                    var foreign = ListSpecs(second).Single(other => other.Tool == spec.Tool);
                    await AssertMcpCursorTransplantAsync(mcp, spec, foreign);
                }

                // A's IDs must not resolve beneath B's program, even for a user who owns both.
                foreach (var spec in ExactSpecs(first))
                {
                    var foreign = ExactSpecs(second).Single(other => other.Tool == spec.Tool);
                    var foreignPath = foreign.Path.Replace(foreign.RecordId.ToString(),
                        spec.RecordId.ToString(), StringComparison.Ordinal);
                    using var denied = await owner.GetAsync(foreignPath);
                    Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
                    var foreignArgs = new Dictionary<string, object?>(foreign.Args)
                    {
                        [foreign.RecordField] = spec.RecordId,
                    };
                    _ = await mcp.When(foreign.Tool, foreignArgs).ExpectFailure("NotFound");
                }
                foreach (var spec in ListSpecs(first))
                {
                    var foreignPath = spec.Path.Replace($"/tenants/{first.TenantId}/",
                        $"/tenants/{second.TenantId}/", StringComparison.Ordinal);
                    using var denied = await owner.GetAsync(foreignPath);
                    Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
                    var foreignArgs = new Dictionary<string, object?>(spec.Args)
                    {
                        ["tenant_id"] = second.TenantId,
                    };
                    _ = await mcp.When(spec.Tool, foreignArgs).ExpectFailure("NotFound");
                }
                foreach (var spec in ListSpecs(first).Where(item => item.ParentId is not null))
                {
                    var foreign = ListSpecs(second).Single(other => other.Tool == spec.Tool);
                    var foreignPath = foreign.Path.Replace(foreign.ParentId!.Value.ToString(),
                        spec.ParentId!.Value.ToString(), StringComparison.Ordinal);
                    using var denied = await owner.GetAsync(foreignPath);
                    Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
                    var foreignArgs = new Dictionary<string, object?>(foreign.Args)
                    {
                        [foreign.ParentField!] = spec.ParentId,
                    };
                    _ = await mcp.When(foreign.Tool, foreignArgs).ExpectFailure("NotFound");
                }

                await using var outsiderMcp = await McpScenario.ConnectAsync(outsider,
                    new Uri(outsider.BaseAddress!, "/mcp"));
                foreach (var spec in ListSpecs(second))
                {
                    using var denied = await outsider.GetAsync(spec.Path);
                    Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
                    _ = await outsiderMcp.When(spec.Tool, spec.Args).ExpectFailure("NotFound");
                }
                foreach (var spec in ExactSpecs(second))
                {
                    using var denied = await outsider.GetAsync(spec.Path);
                    Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
                    _ = await outsiderMcp.When(spec.Tool, spec.Args).ExpectFailure("NotFound");
                }
            }
        }
        finally
        {
            if (worker is not null)
                await worker.StopAsync();
        }
    }

    static async Task<Seed> SeedAsync(HttpClient owner, string label)
    {
        using var tenantResponse = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = $"Draft read tenant {label}",
            slug = $"draft-read-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, tenantResponse.StatusCode);
        var tenantId = Guid.Parse((await ReadAsync(tenantResponse))
            .GetProperty("tenant_id").GetString()!);
        var programId = await CreateProgramAsync(owner, tenantId);
        var servicePath = $"/api/v1/tenants/{tenantId}/programs/{programId}/client-services";
        using var serviceResponse = await owner.PostAsJsonAsync(servicePath, new
        {
            name = $"Client service {label}",
            purpose = "Provide the contracted service",
            owner_reference = "Operations",
        });
        Assert.Equal(HttpStatusCode.OK, serviceResponse.StatusCode);
        var serviceId = Guid.Parse((await ReadAsync(serviceResponse))
            .GetProperty("service_id").GetString()!);

        var controls = new Guid[2];
        var commitments = new Guid[2];
        var risks = new Guid[2];
        var prefix = $"/api/v1/tenants/{tenantId}/programs/{programId}";
        for (var index = 0; index < 2; index++)
        {
            using var control = await owner.PostAsJsonAsync(prefix + "/controls", new
            {
                identifier = $"AC-{label}-{index}",
                content = ControlContent($"Access review {label} {index}"),
            });
            Assert.Equal(HttpStatusCode.OK, control.StatusCode);
            controls[index] = Guid.Parse((await ReadAsync(control))
                .GetProperty("control_id").GetString()!);

            using var commitment = await owner.PostAsJsonAsync(prefix + "/commitment-drafts",
                new
                {
                    service_id = serviceId,
                    kind = "service_commitment",
                    identifier = $"SC-{label}-{index}",
                    statement = $"Service promise {label} {index}",
                    context = "Draft",
                    source_reference = "Contract section 4",
                });
            Assert.Equal(HttpStatusCode.OK, commitment.StatusCode);
            commitments[index] = Guid.Parse((await ReadAsync(commitment))
                .GetProperty("draft_id").GetString()!);

            using var risk = await owner.PostAsJsonAsync(prefix + "/risks", new
            {
                identifier = $"R-{label}-{index}",
                title = $"Provider risk {label} {index}",
                scenario = "Provider becomes unavailable",
                potential_effect = "Service requests cannot be processed",
                source_note = "Management observation",
            });
            Assert.Equal(HttpStatusCode.OK, risk.StatusCode);
            risks[index] = Guid.Parse((await ReadAsync(risk))
                .GetProperty("risk_id").GetString()!);
        }

        using (var control = await owner.PutAsJsonAsync(
                   $"{prefix}/controls/{controls[0]}/draft", new
                   {
                       expected_revision = 1,
                       content = ControlContent($"Revised access review {label}"),
                   }))
            Assert.Equal(HttpStatusCode.NoContent, control.StatusCode);
        using (var commitment = await owner.PutAsJsonAsync(
                   $"{prefix}/commitment-drafts/{commitments[0]}", new
                   {
                       expected_revision = 1,
                       statement = $"Revised service promise {label}",
                       context = "Still draft",
                       source_reference = "Contract section 5",
                   }))
            Assert.Equal(HttpStatusCode.NoContent, commitment.StatusCode);
        using (var risk = await owner.PutAsJsonAsync(
                   $"{prefix}/risks/{risks[0]}/draft", new
                   {
                       expected_revision = 1,
                       title = $"Revised provider risk {label}",
                       scenario = "Provider becomes unavailable",
                       potential_effect = "Service requests cannot be processed",
                       source_note = "Management observation",
                   }))
            Assert.Equal(HttpStatusCode.NoContent, risk.StatusCode);

        var seed = new Seed(label, tenantId, programId, controls, commitments, risks);
        foreach (var spec in ExactSpecs(seed).Where(spec => spec.Revision == 2))
            await WaitForHttpAsync(owner, spec.Path + "?minimum_revision=2");
        foreach (var spec in ListSpecs(seed).Where(spec => spec.ParentId is not null))
        {
            var minimum = spec.Tool switch
            {
                "bdgrz.control.draft.revisions.list" => "minimum_control_draft_revision",
                "bdgrz.commitment.draft.revision.list" => "minimum_draft_revision",
                _ => "minimum_risk_revision",
            };
            await WaitForHttpAsync(owner, spec.Path + "?" + minimum + "=2");
        }
        foreach (var spec in ListSpecs(seed).Where(spec => spec.ParentId is null))
            await WaitForHttpAsync(owner, spec.Path);
        return seed;
    }

    static ListSpec[] ListSpecs(Seed seed)
    {
        var prefix = $"/api/v1/tenants/{seed.TenantId}/programs/{seed.ProgramId}";
        Dictionary<string, object?> Input() => new()
        {
            ["tenant_id"] = seed.TenantId,
            ["program_id"] = seed.ProgramId,
        };
        Dictionary<string, object?> History(string field, Guid value)
        {
            var input = Input();
            input[field] = value;
            return input;
        }
        return
        [
            new(prefix + "/controls", "bdgrz.control.draft.list", Input(),
                "control_id", seed.Controls.Select(id => id.ToString()).ToArray(), seed),
            new(prefix + "/commitment-drafts", "bdgrz.commitment.draft.list", Input(),
                "draft_id", seed.Commitments.Select(id => id.ToString()).ToArray(), seed),
            new(prefix + "/risks", "bdgrz.risk.draft.list", Input(),
                "risk_id", seed.Risks.Select(id => id.ToString()).ToArray(), seed),
            new($"{prefix}/controls/{seed.Controls[0]}/draft/revisions",
                "bdgrz.control.draft.revisions.list",
                History("control_id", seed.Controls[0]), "revision", ["1", "2"], seed,
                seed.Controls[0], "control_id"),
            new($"{prefix}/commitment-drafts/{seed.Commitments[0]}/revisions",
                "bdgrz.commitment.draft.revision.list",
                History("draft_id", seed.Commitments[0]), "revision", ["1", "2"], seed,
                seed.Commitments[0], "draft_id"),
            new($"{prefix}/risks/{seed.Risks[0]}/draft/revisions",
                "bdgrz.risk.draft.revisions.list",
                History("risk_id", seed.Risks[0]), "revision", ["1", "2"], seed,
                seed.Risks[0], "risk_id"),
        ];
    }

    static ExactSpec[] ExactSpecs(Seed seed)
    {
        var prefix = $"/api/v1/tenants/{seed.TenantId}/programs/{seed.ProgramId}";
        Dictionary<string, object?> Input(string field, Guid value, long? revision = null)
        {
            var input = new Dictionary<string, object?>
            {
                ["tenant_id"] = seed.TenantId,
                ["program_id"] = seed.ProgramId,
                [field] = value,
            };
            if (revision is not null)
                input["revision"] = revision;
            return input;
        }
        return
        [
            new($"{prefix}/controls/{seed.Controls[0]}/draft",
                "bdgrz.control.draft.get", Input("control_id", seed.Controls[0]),
                seed, seed.Controls[0], "control_id", 2),
            new($"{prefix}/commitment-drafts/{seed.Commitments[0]}",
                "bdgrz.commitment.draft.get", Input("draft_id", seed.Commitments[0]),
                seed, seed.Commitments[0], "draft_id", 2),
            new($"{prefix}/risks/{seed.Risks[0]}/draft",
                "bdgrz.risk.draft.get", Input("risk_id", seed.Risks[0]),
                seed, seed.Risks[0], "risk_id", 2),
            new($"{prefix}/controls/{seed.Controls[0]}/draft/revisions/1",
                "bdgrz.control.draft.revision.get",
                Input("control_id", seed.Controls[0], 1),
                seed, seed.Controls[0], "control_id", 1),
            new($"{prefix}/commitment-drafts/{seed.Commitments[0]}/revisions/1",
                "bdgrz.commitment.draft.revision.get",
                Input("draft_id", seed.Commitments[0], 1),
                seed, seed.Commitments[0], "draft_id", 1),
            new($"{prefix}/risks/{seed.Risks[0]}/draft/revisions/1",
                "bdgrz.risk.draft.revision.get",
                Input("risk_id", seed.Risks[0], 1),
                seed, seed.Risks[0], "risk_id", 1),
        ];
    }

    static async Task AssertHttpPagesAsync(HttpClient owner, ListSpec spec)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? cursor = null;
        var pages = 0;
        do
        {
            Assert.True(pages++ < 20, $"HTTP cursor did not terminate: {spec.Tool}");
            var path = spec.Path + "?limit=1" +
                (cursor is null ? string.Empty : "&cursor=" + Uri.EscapeDataString(cursor));
            using var response = await owner.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var page = await ReadAsync(response);
            AssertPage(page, spec, seen);
            cursor = page.GetProperty("next_cursor").GetString();
        } while (cursor is not null);
        Assert.Equal(spec.ExpectedIds.Order(StringComparer.OrdinalIgnoreCase),
            seen.Order(StringComparer.OrdinalIgnoreCase));
    }

    static async Task AssertMcpPagesAsync(McpScenario mcp, ListSpec spec)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? cursor = null;
        var pages = 0;
        do
        {
            Assert.True(pages++ < 20, $"MCP cursor did not terminate: {spec.Tool}");
            var args = new Dictionary<string, object?>(spec.Args) { ["limit"] = 1 };
            if (cursor is not null)
                args["cursor"] = cursor;
            var page = await ReadToolAsync(mcp, spec.Tool, args);
            AssertPage(page, spec, seen);
            cursor = page.GetProperty("next_cursor").GetString();
        } while (cursor is not null);
        Assert.Equal(spec.ExpectedIds.Order(StringComparer.OrdinalIgnoreCase),
            seen.Order(StringComparer.OrdinalIgnoreCase));
    }

    static async Task AssertHttpCursorTransplantAsync(HttpClient owner,
        ListSpec from, ListSpec to)
    {
        var first = await ReadHttpAsync(owner, from.Path + "?limit=1");
        var cursor = first.GetProperty("next_cursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(cursor));
        using var response = await owner.GetAsync(to.Path + "?limit=1&cursor=" +
            Uri.EscapeDataString(cursor));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    static async Task AssertMcpCursorTransplantAsync(McpScenario mcp,
        ListSpec from, ListSpec to)
    {
        var fromArgs = new Dictionary<string, object?>(from.Args) { ["limit"] = 1 };
        var first = await ReadToolAsync(mcp, from.Tool, fromArgs);
        var cursor = first.GetProperty("next_cursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(cursor));
        var toArgs = new Dictionary<string, object?>(to.Args)
        {
            ["limit"] = 1,
            ["cursor"] = cursor,
        };
        _ = await mcp.When(to.Tool, toArgs).ExpectFailure("Validation");
    }

    static void AssertPage(JsonElement page, ListSpec spec, HashSet<string> seen)
    {
        foreach (var item in page.GetProperty("items").EnumerateArray())
        {
            Assert.Equal(spec.Seed.TenantId.ToString(), item.GetProperty("tenant_id").GetString());
            Assert.Equal(spec.Seed.ProgramId.ToString(), item.GetProperty("program_id").GetString());
            if (spec.ParentId is { } parent)
                Assert.Equal(parent.ToString(), item.GetProperty(spec.ParentField!).GetString());
            var id = item.GetProperty(spec.IdField).ToString();
            Assert.Contains(id, spec.ExpectedIds, StringComparer.OrdinalIgnoreCase);
            Assert.True(seen.Add(id), $"Duplicate draft read row: {spec.Tool} {id}");
            AssertContentBelongsTo(item, spec.Seed);
        }
    }

    static void AssertExact(JsonElement view, ExactSpec spec)
    {
        Assert.Equal(spec.Seed.TenantId.ToString(), view.GetProperty("tenant_id").GetString());
        Assert.Equal(spec.Seed.ProgramId.ToString(), view.GetProperty("program_id").GetString());
        Assert.Equal(spec.RecordId.ToString(), view.GetProperty(spec.RecordField).GetString());
        Assert.Equal(spec.Revision, view.GetProperty("revision").GetInt64());
        AssertContentBelongsTo(view, spec.Seed);
    }

    static void AssertContentBelongsTo(JsonElement view, Seed seed)
    {
        Assert.Contains($"-{seed.Label}-", view.GetProperty("identifier").GetString(),
            StringComparison.Ordinal);
        var text = view.TryGetProperty("statement", out var statement)
            ? statement.GetString()
            : view.GetProperty("content").GetProperty("title").GetString();
        Assert.Contains($" {seed.Label}", text, StringComparison.Ordinal);
    }

    static async Task<JsonElement> ReadHttpAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    static async Task<JsonElement> ReadToolAsync(McpScenario mcp, string tool,
        Dictionary<string, object?> args)
    {
        var call = await mcp.When(tool, args).ExpectSuccess();
        return Assert.IsType<JsonElement>(call.StructuredJson).GetProperty("result");
    }

    static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    static async Task WaitForHttpAsync(HttpClient client, string path)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
                return;
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await Task.Delay(250);
        }
        throw new TimeoutException($"Draft projection did not catch up: {path}");
    }

    static async Task<Guid> CreateProgramAsync(HttpClient owner, Guid tenantId)
    {
        var path = $"/api/v1/tenants/{tenantId}/programs";
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(path, new
            {
                name = "SOC 2",
                plan = new
                {
                    target_readiness_date = (string?)null,
                    target_type_i_as_of_date = (string?)null,
                    target_type_ii_start_date = (string?)null,
                    target_type_ii_end_date = (string?)null,
                    readiness_advisor = (string?)null,
                    audit_firm = (string?)null,
                },
            });
            if (response.StatusCode == HttpStatusCode.OK)
                return Guid.Parse((await ReadAsync(response)).GetProperty("program_id").GetString()!);
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("Program creation never became authorized.");
    }

    static object ControlContent(string title) => new
    {
        title,
        objective = "Ensure access is reviewed",
        description = "People with privileged access are reviewed.",
        implementation_narrative = "The security lead reviews the access listing.",
        expected_evidence_descriptions = new[] { "Review record", "Access listing" },
    };

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

    sealed record Seed(string Label, Guid TenantId, Guid ProgramId, Guid[] Controls,
        Guid[] Commitments, Guid[] Risks);

    sealed record ListSpec(string Path, string Tool, Dictionary<string, object?> Args,
        string IdField, string[] ExpectedIds, Seed Seed, Guid? ParentId = null,
        string? ParentField = null);

    sealed record ExactSpec(string Path, string Tool, Dictionary<string, object?> Args,
        Seed Seed, Guid RecordId, string RecordField, long Revision);
}
