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
                await using var mcp = await McpScenario.ConnectAsync(owner,
                    new Uri(owner.BaseAddress!, "/mcp"));
                await using var outsiderMcp = await McpScenario.ConnectAsync(outsider,
                    new Uri(outsider.BaseAddress!, "/mcp"));

                // Act: collect every positive read, cursor transplant, and denial probe.
                var listWalks = new List<ListWalk>();
                var exactReads = new List<ExactRead>();
                foreach (var seed in new[] { first, second })
                {
                    foreach (var spec in ListSpecs(seed))
                    {
                        listWalks.Add(await WalkHttpPagesAsync(owner, spec));
                        listWalks.Add(await WalkMcpPagesAsync(mcp, spec));
                    }
                    foreach (var spec in ExactSpecs(seed))
                    {
                        exactReads.Add(new(spec, "HTTP", await ReadHttpAsync(owner, spec.Path)));
                        exactReads.Add(new(spec, "MCP",
                            await ReadToolAsync(mcp, spec.Tool, spec.Args)));
                    }
                }
                var transplants = new List<ProbeResult>();
                foreach (var spec in ListSpecs(first))
                {
                    var foreign = ListSpecs(second).Single(other => other.Tool == spec.Tool);
                    transplants.Add(await ProbeCursorTransplantAsync(owner, mcp, spec, foreign));
                }
                var ownerProbes = new List<ProbeResult>();
                foreach (var probe in DenialProbes(first, second))
                    ownerProbes.Add(await RunProbeAsync(owner, mcp, probe));
                var outsiderProbes = new List<ProbeResult>();
                foreach (var probe in ListSpecs(second).Select(spec => spec.AsProbe("outsider"))
                             .Concat(ExactSpecs(second).Select(spec => spec.AsProbe("outsider"))))
                    outsiderProbes.Add(await RunProbeAsync(outsider, outsiderMcp, probe));

                // Assert: every populated list is exhausted, including an empty terminal page.
                Assert.Equal(2 * 2 * 6, listWalks.Count);
                foreach (var walk in listWalks)
                    AssertWalk(walk);
                Assert.Equal(2 * 2 * 6, exactReads.Count);
                foreach (var read in exactReads)
                    AssertExact(read);
                foreach (var result in transplants)
                    AssertDenied(result, HttpStatusCode.BadRequest, "Validation");
                // A member of both tenants cannot swap tenant, program, or record IDs.
                Assert.Equal(6 + 3 + 12 + 12, ownerProbes.Count);
                foreach (var result in ownerProbes)
                    AssertDenied(result, HttpStatusCode.NotFound, "NotFound");
                Assert.Equal(12, outsiderProbes.Count);
                foreach (var result in outsiderProbes)
                    AssertDenied(result, HttpStatusCode.NotFound, "NotFound");
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
            await WaitForListAsync(owner, spec);
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

    static IEnumerable<Probe> DenialProbes(Seed first, Seed second)
    {
        // A's record IDs beneath B's tenant and program.
        foreach (var spec in ExactSpecs(first))
        {
            var foreign = ExactSpecs(second).Single(other => other.Tool == spec.Tool);
            yield return new("record-swap " + spec.Tool,
                foreign.Path.Replace(foreign.RecordId.ToString(), spec.RecordId.ToString(),
                    StringComparison.Ordinal),
                foreign.Tool,
                new Dictionary<string, object?>(foreign.Args)
                {
                    [foreign.RecordField] = spec.RecordId,
                });
        }
        foreach (var spec in ListSpecs(first).Where(item => item.ParentId is not null))
        {
            var foreign = ListSpecs(second).Single(other => other.Tool == spec.Tool);
            yield return new("record-swap " + spec.Tool,
                foreign.Path.Replace(foreign.ParentId!.Value.ToString(),
                    spec.ParentId!.Value.ToString(), StringComparison.Ordinal),
                foreign.Tool,
                new Dictionary<string, object?>(foreign.Args)
                {
                    [foreign.ParentField!] = spec.ParentId,
                });
        }

        // B's tenant with A's program and record IDs; A's tenant with B's program.
        var specs = ListSpecs(first).Select(spec => (spec.Path, spec.Tool, spec.Args))
            .Concat(ExactSpecs(first).Select(spec => (spec.Path, spec.Tool, spec.Args)));
        foreach (var (path, tool, args) in specs)
        {
            yield return new("tenant-swap " + tool,
                path.Replace($"/tenants/{first.TenantId}/", $"/tenants/{second.TenantId}/",
                    StringComparison.Ordinal),
                tool,
                new Dictionary<string, object?>(args) { ["tenant_id"] = second.TenantId });
            yield return new("program-swap " + tool,
                path.Replace($"/programs/{first.ProgramId}/", $"/programs/{second.ProgramId}/",
                    StringComparison.Ordinal)
                    .Replace($"/programs/{first.ProgramId}", $"/programs/{second.ProgramId}",
                        StringComparison.Ordinal),
                tool,
                new Dictionary<string, object?>(args) { ["program_id"] = second.ProgramId });
        }
    }

    static async Task<ProbeResult> RunProbeAsync(HttpClient client, McpScenario mcp, Probe probe)
    {
        using var response = await client.GetAsync(probe.HttpPath);
        var httpBody = await response.Content.ReadAsStringAsync();
        var call = await mcp.When(probe.Tool, probe.Args);
        // Failures carry Portia's details in result metadata; successes carry structured content.
        var structured = Assert.IsType<JsonElement>(call.IsError ? call.Error : call.StructuredJson);
        return new(probe, response.StatusCode, httpBody, call.IsError,
            call.IsError ? structured.GetProperty("kind").GetString() : null,
            structured.GetRawText());
    }

    static async Task<ProbeResult> ProbeCursorTransplantAsync(HttpClient owner,
        McpScenario mcp, ListSpec from, ListSpec to)
    {
        var httpCursor = (await ReadHttpAsync(owner, from.Path + "?limit=1"))
            .GetProperty("next_cursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(httpCursor));
        var fromArgs = new Dictionary<string, object?>(from.Args) { ["limit"] = 1 };
        var mcpCursor = (await ReadToolAsync(mcp, from.Tool, fromArgs))
            .GetProperty("next_cursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(mcpCursor));
        return await RunProbeAsync(owner, mcp, new("cursor-transplant " + from.Tool,
            to.Path + "?limit=1&cursor=" + Uri.EscapeDataString(httpCursor),
            to.Tool,
            new Dictionary<string, object?>(to.Args) { ["limit"] = 1, ["cursor"] = mcpCursor }));
    }

    static void AssertDenied(ProbeResult result, HttpStatusCode status, string kind)
    {
        Assert.True(result.HttpStatus == status,
            $"{result.Probe.Name} HTTP {result.Probe.HttpPath} returned " +
            $"{(int)result.HttpStatus}: {result.HttpBody}");
        Assert.True(result.McpIsError && result.McpKind == kind,
            $"{result.Probe.Name} MCP {result.Probe.Tool} returned {result.McpBody}");
    }

    static async Task<ListWalk> WalkHttpPagesAsync(HttpClient owner, ListSpec spec)
    {
        var pages = new List<JsonElement>();
        string? cursor = null;
        do
        {
            Assert.True(pages.Count < 20, $"HTTP cursor did not terminate: {spec.Tool}");
            var path = spec.Path + "?limit=1" +
                (cursor is null ? string.Empty : "&cursor=" + Uri.EscapeDataString(cursor));
            var page = await ReadHttpAsync(owner, path);
            pages.Add(page);
            cursor = page.GetProperty("next_cursor").GetString();
        } while (cursor is not null);
        return new(spec, "HTTP", pages);
    }

    static async Task<ListWalk> WalkMcpPagesAsync(McpScenario mcp, ListSpec spec)
    {
        var pages = new List<JsonElement>();
        string? cursor = null;
        do
        {
            Assert.True(pages.Count < 20, $"MCP cursor did not terminate: {spec.Tool}");
            var args = new Dictionary<string, object?>(spec.Args) { ["limit"] = 1 };
            if (cursor is not null)
                args["cursor"] = cursor;
            var page = await ReadToolAsync(mcp, spec.Tool, args);
            pages.Add(page);
            cursor = page.GetProperty("next_cursor").GetString();
        } while (cursor is not null);
        return new(spec, "MCP", pages);
    }

    static void AssertWalk(ListWalk walk)
    {
        var spec = walk.Spec;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in walk.Pages)
            AssertPage(page, spec, seen);
        Assert.Equal(spec.ExpectedIds.Order(StringComparer.OrdinalIgnoreCase),
            seen.Order(StringComparer.OrdinalIgnoreCase));
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

    static void AssertExact(ExactRead read)
    {
        var (spec, view) = (read.Spec, read.View);
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

    static async Task WaitForListAsync(HttpClient client, ListSpec spec)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(spec.Path + "?limit=100");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var ids = (await ReadAsync(response)).GetProperty("items").EnumerateArray()
                    .Select(item => item.GetProperty(spec.IdField).ToString())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (spec.ExpectedIds.All(ids.Contains))
                    return;
            }
            else
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await Task.Delay(250);
        }
        throw new TimeoutException($"Draft list did not include every seeded record: {spec.Path}");
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
        string? ParentField = null)
    {
        public Probe AsProbe(string name) => new($"{name} {Tool}", Path, Tool, Args);
    }

    sealed record ExactSpec(string Path, string Tool, Dictionary<string, object?> Args,
        Seed Seed, Guid RecordId, string RecordField, long Revision)
    {
        public Probe AsProbe(string name) => new($"{name} {Tool}", Path, Tool, Args);
    }

    sealed record ListWalk(ListSpec Spec, string Transport, List<JsonElement> Pages);

    sealed record ExactRead(ExactSpec Spec, string Transport, JsonElement View);

    sealed record Probe(string Name, string HttpPath, string Tool,
        Dictionary<string, object?> Args);

    sealed record ProbeResult(Probe Probe, HttpStatusCode HttpStatus, string HttpBody,
        bool McpIsError, string? McpKind, string McpBody);
}
