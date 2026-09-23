using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     EN-01: two populated tenants must remain separate across application history, instance,
///     boundary-reference, and change-preview reads in standalone and split API/worker hosts.
/// </summary>
[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ApplicationReadLeakMatrixE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    static readonly string[] SecurityCategory = ["security"];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldKeepApplicationReadPagesAndPreviewsWithinTenantGivenTwoPopulatedTenants(
        bool splitHosts)
    {
        // Arrange
        var applicationName = $"compliance-application-leak-{Guid.NewGuid():N}";
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
                    $"application-matrix-owner-{Guid.NewGuid():N}@example.com");
                await TenantInvitationE2ETests.LoginAsync(outsider,
                    $"application-matrix-outsider-{Guid.NewGuid():N}@example.com");
                var first = await SeedAsync(owner, "A");
                var second = await SeedAsync(owner, "B");

                // Act
                // Assert: every page, including a cursor carried to the other tenant,
                // has only that tenant's positive records.
                foreach (var tenant in new[] { first, second })
                {
                    foreach (var spec in ReadSpecs(tenant))
                        await AssertHttpPagesAsync(owner, spec);
                    using (var revision = await owner.GetAsync(
                               $"/api/v1/tenants/{tenant.TenantId}/applications/" +
                               $"{tenant.ApplicationId}/revisions/3"))
                    using (var instance = await owner.GetAsync(
                               $"/api/v1/tenants/{tenant.TenantId}/applications/" +
                               $"{tenant.ApplicationId}/system-instances/{tenant.Instances[0]}"))
                    {
                        Assert.Equal(HttpStatusCode.OK, revision.StatusCode);
                        Assert.Equal(HttpStatusCode.OK, instance.StatusCode);
                        AssertExactBelongsTo(await ReadAsync(revision), tenant,
                            "application_id", tenant.ApplicationId);
                        AssertExactBelongsTo(await ReadAsync(instance), tenant,
                            "system_instance_id", tenant.Instances[0]);
                    }
                    await AssertHttpPreviewAsync(owner, tenant, first, second);
                }

                foreach (var spec in ReadSpecs(first))
                {
                    using var firstPage = await owner.GetAsync(spec.Path + "?limit=1");
                    Assert.Equal(HttpStatusCode.OK, firstPage.StatusCode);
                    var cursor = (await ReadAsync(firstPage)).GetProperty("next_cursor").GetString();
                    Assert.False(string.IsNullOrWhiteSpace(cursor));
                    var foreignSpec = ReadSpecs(second).Single(other => other.Tool == spec.Tool);
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (var pageIndex = 0; pageIndex < foreignSpec.ExpectedIds.Length + 2; pageIndex++)
                    {
                        using var foreignPage = await owner.GetAsync(foreignSpec.Path +
                            "?limit=1&cursor=" + Uri.EscapeDataString(cursor));
                        if (pageIndex == 0 && foreignPage.StatusCode == HttpStatusCode.BadRequest)
                        {
                            cursor = null;
                            break;
                        }
                        Assert.Equal(HttpStatusCode.OK, foreignPage.StatusCode);
                        var page = await ReadAsync(foreignPage);
                        AssertPageBelongsTo(page, foreignSpec);
                        foreach (var item in page.GetProperty("items").EnumerateArray())
                            Assert.True(seen.Add(item.GetProperty(foreignSpec.IdProperty).ToString()));
                        cursor = page.GetProperty("next_cursor").GetString();
                        if (cursor is null)
                            break;
                    }
                    Assert.Null(cursor);
                }

                await using (var mcp = await McpScenario.ConnectAsync(owner,
                                 new Uri(owner.BaseAddress!, "/mcp")))
                {
                    foreach (var tenant in new[] { first, second })
                    {
                        foreach (var spec in ReadSpecs(tenant))
                        {
                            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            string? cursor = null;
                            var pageCount = 0;
                            do
                            {
                                Assert.True(pageCount++ < 50, "MCP application read cursor did not terminate.");
                                var args = new Dictionary<string, object?>(spec.Args)
                                {
                                    ["limit"] = 1,
                                };
                                if (cursor is not null)
                                    args["cursor"] = cursor;
                                var call = await mcp.When(spec.Tool, args).ExpectSuccess();
                                var page = Assert.IsType<JsonElement>(call.StructuredJson)
                                    .GetProperty("result");
                                AssertPageBelongsTo(page, spec);
                                foreach (var item in page.GetProperty("items").EnumerateArray())
                                    Assert.True(seen.Add(item.GetProperty(spec.IdProperty).ToString()));
                                cursor = page.GetProperty("next_cursor").GetString();
                            } while (cursor is not null && seen.Count < spec.ExpectedIds.Length);
                            Assert.Equal(spec.ExpectedIds.Order(StringComparer.OrdinalIgnoreCase),
                                seen.Order(StringComparer.OrdinalIgnoreCase));
                            Assert.Null(cursor);
                        }

                        var previewCall = await mcp.When("bdgrz.application.change.preview",
                            PreviewArgs(tenant)).ExpectSuccess();
                        AssertPreviewBelongsTo(Assert.IsType<JsonElement>(previewCall.StructuredJson)
                            .GetProperty("result"), tenant, first, second);
                        var revisionCall = await mcp.When("bdgrz.application.revision.get",
                            new Dictionary<string, object?>
                            {
                                ["tenant_id"] = tenant.TenantId,
                                ["application_id"] = tenant.ApplicationId,
                                ["revision"] = 3,
                            }).ExpectSuccess();
                        AssertExactBelongsTo(Assert.IsType<JsonElement>(revisionCall.StructuredJson)
                            .GetProperty("result"), tenant, "application_id",
                            tenant.ApplicationId);
                        var instanceCall = await mcp.When("bdgrz.system_instance.get",
                            new Dictionary<string, object?>
                            {
                                ["tenant_id"] = tenant.TenantId,
                                ["application_id"] = tenant.ApplicationId,
                                ["system_instance_id"] = tenant.Instances[0],
                            }).ExpectSuccess();
                        AssertExactBelongsTo(Assert.IsType<JsonElement>(instanceCall.StructuredJson)
                            .GetProperty("result"), tenant, "system_instance_id",
                            tenant.Instances[0]);
                    }

                    foreach (var spec in ReadSpecs(first))
                    {
                        var foreignArgs = new Dictionary<string, object?>(spec.Args)
                        {
                            ["tenant_id"] = second.TenantId,
                        };
                        _ = await mcp.When(spec.Tool, foreignArgs).ExpectFailure("NotFound");
                    }
                    _ = await mcp.When("bdgrz.application.change.preview",
                        PreviewArgs(first, second.TenantId)).ExpectFailure("NotFound");
                    _ = await mcp.When("bdgrz.application.revision.get",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = second.TenantId,
                            ["application_id"] = first.ApplicationId,
                            ["revision"] = 1,
                        }).ExpectFailure("NotFound");
                    _ = await mcp.When("bdgrz.system_instance.get",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = second.TenantId,
                            ["application_id"] = second.ApplicationId,
                            ["system_instance_id"] = first.Instances[0],
                        }).ExpectFailure("NotFound");
                    _ = await mcp.When("bdgrz.system_instance.boundary_references.list",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = second.TenantId,
                            ["application_id"] = second.ApplicationId,
                            ["system_instance_id"] = first.Instances[0],
                        }).ExpectFailure("NotFound");
                }

                await using (var outsiderMcp = await McpScenario.ConnectAsync(outsider,
                                 new Uri(outsider.BaseAddress!, "/mcp")))
                {
                    foreach (var spec in ReadSpecs(first))
                        _ = await outsiderMcp.When(spec.Tool, spec.Args)
                            .ExpectFailure("NotFound");
                    _ = await outsiderMcp.When("bdgrz.application.change.preview",
                        PreviewArgs(first)).ExpectFailure("NotFound");
                    _ = await outsiderMcp.When("bdgrz.application.revision.get",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = first.TenantId,
                            ["application_id"] = first.ApplicationId,
                            ["revision"] = 3,
                        }).ExpectFailure("NotFound");
                    _ = await outsiderMcp.When("bdgrz.system_instance.get",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = first.TenantId,
                            ["application_id"] = first.ApplicationId,
                            ["system_instance_id"] = first.Instances[0],
                        }).ExpectFailure("NotFound");
                }

                await AssertHttpDenialsAsync(owner, outsider, first, second);
            }
        }
        finally
        {
            if (worker is not null)
                await worker.StopAsync();
        }
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

    static async Task<Seed> SeedAsync(HttpClient owner, string label)
    {
        var tenantId = Guid.Parse((await PostUntilAuthorizedAsync(owner,
            "/api/v1/tenants", new
            {
                name = "Application matrix " + label,
                slug = $"app-matrix-{Guid.NewGuid():N}"[..24],
            })).GetProperty("tenant_id").GetString()!);
        var applicationsPath = $"/api/v1/tenants/{tenantId}/applications";
        var applicationId = Guid.Parse((await PostUntilAuthorizedAsync(owner, applicationsPath,
            new { name = "Payroll " + label, purpose = "Run payroll " + label }))
            .GetProperty("application_id").GetString()!);
        var applicationPath = $"{applicationsPath}/{applicationId}";
        _ = await WaitForOkAsync(() => owner.GetAsync(applicationPath));
        var instances = new Guid[2];
        for (var index = 0; index < instances.Length; index++)
        {
            using var declared = await owner.PostAsJsonAsync(applicationPath + "/system-instances",
                new
                {
                    expected_application_revision = index + 1,
                    name = $"Payroll {label} instance {index}",
                    kind = "production",
                    source_identifier = $"payroll-{label}-{index}",
                });
            Assert.Equal(HttpStatusCode.OK, declared.StatusCode);
            instances[index] = Guid.Parse((await ReadAsync(declared))
                .GetProperty("system_instance_id").GetString()!);
        }
        _ = await WaitForOkAsync(() => owner.GetAsync(applicationPath + "/revisions/3"));
        _ = await WaitForOkAsync(() => owner.GetAsync(
            applicationPath + "/system-instances?minimum_application_revision=3"));

        var programId = Guid.Parse((await PostUntilAuthorizedAsync(owner,
            $"/api/v1/tenants/{tenantId}/programs", new
            {
                name = "Application matrix " + label,
                plan = new
                {
                    target_readiness_date = "2027-01-31",
                    target_type_i_as_of_date = "2027-03-31",
                    target_type_ii_start_date = "2027-04-01",
                    target_type_ii_end_date = "2028-03-31",
                    readiness_advisor = "Advisor",
                    audit_firm = (string?)null,
                },
            })).GetProperty("program_id").GetString()!);
        var boundaryPath = $"/api/v1/tenants/{tenantId}/programs/{programId}/boundaries";
        var applicationBoundaries = new Guid[2];
        var instanceBoundaries = new Guid[2];
        for (var index = 0; index < 2; index++)
        {
            applicationBoundaries[index] = await CreateBoundaryAsync(owner, boundaryPath,
                "application", applicationId);
            instanceBoundaries[index] = await CreateBoundaryAsync(owner, boundaryPath,
                "system_instance", instances[0]);
        }
        await WaitForItemsAsync(owner, applicationPath + "/boundary-references", 2);
        await WaitForItemsAsync(owner, applicationPath + "/system-instances/" + instances[0] +
            "/boundary-references", 2);
        _ = await WaitForOkAsync(() => owner.PostAsJsonAsync(
            applicationPath + "/change-previews", new
            {
                expected_application_revision = 3,
                change_kind = "retire",
            }));
        return new Seed(tenantId, applicationId, instances, applicationBoundaries,
            instanceBoundaries);
    }

    static async Task<Guid> CreateBoundaryAsync(HttpClient owner, string path,
        string subjectType, Guid governedRecordId)
    {
        using var response = await owner.PostAsJsonAsync(path, new
        {
            content = new
            {
                statement = "Application matrix boundary.",
                engagement_stage = "readiness",
                trust_services_categories = SecurityCategory,
                entries = new[]
                {
                    new
                    {
                        entry_id = Guid.NewGuid(),
                        kind = "inclusion",
                        subject_type = subjectType,
                        subject = "Payroll",
                        governed_record_id = governedRecordId,
                        owner_reference = "Operations",
                        rationale = "Declared in scope for tenant read isolation.",
                        unresolved = false,
                    },
                },
            },
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Guid.Parse((await ReadAsync(response)).GetProperty("boundary_id").GetString()!);
    }

    static ReadSpec[] ReadSpecs(Seed seed)
    {
        var applicationPath = $"/api/v1/tenants/{seed.TenantId}/applications/" +
            seed.ApplicationId;
        return
        [
            new(applicationPath + "/revisions", "bdgrz.application.revision.list", "revision",
                ["1", "2", "3"], seed.TenantId, seed.ApplicationId,
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = seed.TenantId,
                    ["application_id"] = seed.ApplicationId,
                    ["minimum_application_revision"] = 3,
                }, "minimum_application_revision=3"),
            new(applicationPath + "/system-instances", "bdgrz.system_instance.list",
                "system_instance_id", seed.Instances.Select(id => id.ToString()).ToArray(),
                seed.TenantId, seed.ApplicationId, new Dictionary<string, object?>
                {
                    ["tenant_id"] = seed.TenantId,
                    ["application_id"] = seed.ApplicationId,
                    ["minimum_application_revision"] = 3,
                }, "minimum_application_revision=3"),
            new(applicationPath + "/boundary-references",
                "bdgrz.application.boundary_references.list", "boundary_id",
                seed.ApplicationBoundaries.Select(id => id.ToString()).ToArray(),
                seed.TenantId, seed.ApplicationId, new Dictionary<string, object?>
                {
                    ["tenant_id"] = seed.TenantId,
                    ["application_id"] = seed.ApplicationId,
                }),
            new(applicationPath + "/system-instances/" + seed.Instances[0] +
                "/boundary-references", "bdgrz.system_instance.boundary_references.list",
                "boundary_id", seed.InstanceBoundaries.Select(id => id.ToString()).ToArray(),
                seed.TenantId, seed.Instances[0], new Dictionary<string, object?>
                {
                    ["tenant_id"] = seed.TenantId,
                    ["application_id"] = seed.ApplicationId,
                    ["system_instance_id"] = seed.Instances[0],
                }),
        ];
    }

    static async Task AssertHttpPagesAsync(HttpClient owner, ReadSpec spec)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? cursor = null;
        var pageCount = 0;
        do
        {
            Assert.True(pageCount++ < 50, "HTTP application read cursor did not terminate.");
            var query = "?limit=1" + (spec.MinimumRevisionQuery is null ? string.Empty :
                "&" + spec.MinimumRevisionQuery) + (cursor is null ? string.Empty :
                "&cursor=" + Uri.EscapeDataString(cursor));
            using var response = await owner.GetAsync(spec.Path + query);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var page = await ReadAsync(response);
            AssertPageBelongsTo(page, spec);
            foreach (var item in page.GetProperty("items").EnumerateArray())
                Assert.True(seen.Add(item.GetProperty(spec.IdProperty).ToString()));
            cursor = page.GetProperty("next_cursor").GetString();
        } while (cursor is not null && seen.Count < spec.ExpectedIds.Length);
        Assert.Equal(spec.ExpectedIds.Order(StringComparer.OrdinalIgnoreCase),
            seen.Order(StringComparer.OrdinalIgnoreCase));
        Assert.Null(cursor);
    }

    static void AssertPageBelongsTo(JsonElement page, ReadSpec spec)
    {
        foreach (var item in page.GetProperty("items").EnumerateArray())
        {
            Assert.Equal(spec.TenantId.ToString(), item.GetProperty("tenant_id").GetString());
            if (item.TryGetProperty("application_id", out var applicationId))
                Assert.Equal(spec.ApplicationOrSubjectId.ToString(), applicationId.GetString());
            if (item.TryGetProperty("governed_record_id", out var governedRecordId))
                Assert.Equal(spec.ApplicationOrSubjectId.ToString(), governedRecordId.GetString());
            Assert.Contains(item.GetProperty(spec.IdProperty).ToString(), spec.ExpectedIds,
                StringComparer.OrdinalIgnoreCase);
        }
    }

    static void AssertExactBelongsTo(JsonElement view, Seed seed,
        string idProperty, Guid expectedId)
    {
        Assert.Equal(seed.TenantId.ToString(), view.GetProperty("tenant_id").GetString());
        Assert.Equal(expectedId.ToString(), view.GetProperty(idProperty).GetString());
        Assert.Equal(seed.ApplicationId.ToString(),
            view.GetProperty("application_id").GetString());
    }

    static async Task AssertHttpPreviewAsync(HttpClient owner, Seed seed,
        Seed first, Seed second)
    {
        using var response = await owner.PostAsJsonAsync(
            $"/api/v1/tenants/{seed.TenantId}/applications/{seed.ApplicationId}/change-previews",
            new { expected_application_revision = 3, change_kind = "retire" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertPreviewBelongsTo(await ReadAsync(response), seed, first, second);
    }

    static void AssertPreviewBelongsTo(JsonElement preview, Seed seed,
        Seed first, Seed second)
    {
        Assert.Equal(seed.TenantId.ToString(), preview.GetProperty("tenant_id").GetString());
        Assert.Equal(seed.ApplicationId.ToString(),
            preview.GetProperty("application_id").GetString());
        Assert.Equal(3, preview.GetProperty("application_revision").GetInt64());
        var references = preview.GetProperty("boundary_references").EnumerateArray().ToArray();
        Assert.Equal(seed.ApplicationBoundaries.Order(), references.Select(item =>
            Guid.Parse(item.GetProperty("boundary_id").GetString()!)).Order());
        Assert.All(references, item =>
        {
            Assert.Equal(seed.TenantId.ToString(), item.GetProperty("tenant_id").GetString());
            Assert.Equal(seed.ApplicationId.ToString(),
                item.GetProperty("governed_record_id").GetString());
        });
        var foreign = seed.TenantId == first.TenantId ? second : first;
        Assert.DoesNotContain(references, item => foreign.ApplicationBoundaries.Contains(
            Guid.Parse(item.GetProperty("boundary_id").GetString()!)));
    }

    static Dictionary<string, object?> PreviewArgs(Seed seed, Guid? tenantId = null) => new()
    {
        ["tenant_id"] = tenantId ?? seed.TenantId,
        ["application_id"] = seed.ApplicationId,
        ["expected_application_revision"] = 3,
        ["change_kind"] = "retire",
    };

    static async Task AssertHttpDenialsAsync(HttpClient owner, HttpClient outsider,
        Seed first, Seed second)
    {
        var firstPath = $"/api/v1/tenants/{first.TenantId}/applications/" +
            first.ApplicationId;
        var foreignPath = $"/api/v1/tenants/{second.TenantId}/applications/" +
            first.ApplicationId;
        foreach (var suffix in new[]
                 {
                     "/revisions/1", "/revisions", "/system-instances/" + first.Instances[0],
                     "/system-instances", "/boundary-references",
                     "/system-instances/" + first.Instances[0] + "/boundary-references",
                 })
        {
            using var foreign = await owner.GetAsync(foreignPath + suffix);
            using var denied = await outsider.GetAsync(firstPath + suffix);
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }
        using (var foreignInstance = await owner.GetAsync(
                   $"/api/v1/tenants/{second.TenantId}/applications/" +
                   $"{second.ApplicationId}/system-instances/{first.Instances[0]}"))
            Assert.Equal(HttpStatusCode.NotFound, foreignInstance.StatusCode);
        using (var foreignInstanceReferences = await owner.GetAsync(
                   $"/api/v1/tenants/{second.TenantId}/applications/" +
                   $"{second.ApplicationId}/system-instances/{first.Instances[0]}/boundary-references"))
            Assert.Equal(HttpStatusCode.NotFound, foreignInstanceReferences.StatusCode);
        using var foreignPreview = await owner.PostAsJsonAsync(foreignPath + "/change-previews",
            new { expected_application_revision = 3, change_kind = "retire" });
        using var deniedPreview = await outsider.PostAsJsonAsync(firstPath + "/change-previews",
            new { expected_application_revision = 3, change_kind = "retire" });
        Assert.Equal(HttpStatusCode.NotFound, foreignPreview.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedPreview.StatusCode);
    }

    static async Task<JsonElement> PostUntilAuthorizedAsync(HttpClient client, string path,
        object body)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.PostAsJsonAsync(path, body);
            if (response.StatusCode == HttpStatusCode.OK)
                return await ReadAsync(response);
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException($"POST {path} remained unauthorized after bootstrap.");
    }

    static async Task<JsonElement> WaitForOkAsync(Func<Task<HttpResponseMessage>> send)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await send();
            if (response.StatusCode == HttpStatusCode.OK)
                return await ReadAsync(response);
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("The application read did not catch up before the deadline.");
    }

    static async Task WaitForItemsAsync(HttpClient owner, string path, int count)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK &&
                (await ReadAsync(response)).GetProperty("items").GetArrayLength() == count)
                return;
            Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException($"GET {path} did not reach {count} items.");
    }

    static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    sealed record Seed(Guid TenantId, Guid ApplicationId, Guid[] Instances,
        Guid[] ApplicationBoundaries, Guid[] InstanceBoundaries);

    sealed record ReadSpec(string Path, string Tool, string IdProperty, string[] ExpectedIds,
        Guid TenantId, Guid ApplicationOrSubjectId, Dictionary<string, object?> Args,
        string? MinimumRevisionQuery = null);
}
