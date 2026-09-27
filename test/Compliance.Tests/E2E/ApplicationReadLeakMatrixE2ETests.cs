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
            HttpClient reviewer;
            try
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE",
                    splitHosts ? "api" : "standalone");
                owner = factory.CreateClient();
                reviewer = factory.CreateClient();
            }
            finally
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", previousMode);
            }

            using (owner)
            using (reviewer)
            using (var outsider = factory.CreateClient())
            {
                await TenantInvitationE2ETests.LoginAsync(owner,
                    $"application-matrix-owner-{Guid.NewGuid():N}@example.com");
                var reviewerEmail = $"application-matrix-reviewer-{Guid.NewGuid():N}@example.com";
                var reviewerId = await TenantInvitationE2ETests.LoginAsync(reviewer, reviewerEmail);
                await TenantInvitationE2ETests.VerifyEmailAsync(factory, reviewer,
                    reviewerId, reviewerEmail,
                    splitHosts ? worker!.Services.GetRequiredService<MockEmailChallengeDelivery>() : null);
                var delivery = splitHosts
                    ? worker!.Services.GetRequiredService<MockTenantInvitationDelivery>()
                    : factory.Services.GetRequiredService<MockTenantInvitationDelivery>();
                await TenantInvitationE2ETests.LoginAsync(outsider,
                    $"application-matrix-outsider-{Guid.NewGuid():N}@example.com");
                var first = await SeedAsync(owner, reviewer, reviewerEmail, delivery, "A");
                var second = await SeedAsync(owner, reviewer, reviewerEmail, delivery, "B");
                var firstAdditionalApplication = await AddApplicationAsync(owner, first,
                    "A additional");
                var secondAdditionalApplication = await AddApplicationAsync(owner, second,
                    "B additional");

                foreach (var tenant in new[] { first, second })
                {
                    var additionalApplication = tenant == first
                        ? firstAdditionalApplication
                        : secondAdditionalApplication;
                    var other = tenant == first ? second : first;
                    await AssertApplicationPagesAsync(owner, tenant, additionalApplication,
                        other.TenantId);
                }

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
                    using var foreignPage = await owner.GetAsync(foreignSpec.Path +
                        "?limit=1&cursor=" + Uri.EscapeDataString(cursor));
                    Assert.Equal(HttpStatusCode.BadRequest, foreignPage.StatusCode);
                }

                await using (var mcp = await McpScenario.ConnectAsync(owner,
                                 new Uri(owner.BaseAddress!, "/mcp")))
                {
                    foreach (var tenant in new[] { first, second })
                    {
                        var additionalApplication = tenant == first
                            ? firstAdditionalApplication
                            : secondAdditionalApplication;
                        var other = tenant == first ? second : first;
                        await AssertApplicationMcpPagesAsync(mcp, tenant,
                            additionalApplication, other.TenantId);
                    }

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
                        var firstArgs = new Dictionary<string, object?>(spec.Args)
                        {
                            ["limit"] = 1,
                        };
                        var firstPage = await mcp.When(spec.Tool, firstArgs).ExpectSuccess();
                        var cursor = Assert.IsType<JsonElement>(firstPage.StructuredJson)
                            .GetProperty("result").GetProperty("next_cursor").GetString();
                        Assert.False(string.IsNullOrWhiteSpace(cursor));
                        var foreignSpec = ReadSpecs(second).Single(other => other.Tool == spec.Tool);
                        var foreignArgs = new Dictionary<string, object?>(foreignSpec.Args)
                        {
                            ["limit"] = 1,
                            ["cursor"] = cursor,
                        };
                        _ = await mcp.When(foreignSpec.Tool, foreignArgs)
                            .ExpectFailure("Validation");
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

    static async Task<Seed> SeedAsync(HttpClient owner, HttpClient reviewer,
        string reviewerEmail, MockTenantInvitationDelivery delivery, string label)
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
                    expected_application_revision = 1,
                    name = $"Payroll {label} instance {index}",
                    kind = "production",
                    source_identifier = $"payroll-{label}-{index}",
                });
            Assert.Equal(HttpStatusCode.OK, declared.StatusCode);
            instances[index] = Guid.Parse((await ReadAsync(declared))
                .GetProperty("system_instance_id").GetString()!);
        }
        // Instance declarations no longer revise the application; build metadata history directly.
        for (var revision = 1; revision <= 2; revision++)
        {
            using var revised = await owner.PutAsJsonAsync(applicationPath, new
            {
                expected_revision = revision,
                name = "Payroll " + label,
                purpose = "Run payroll " + label,
                owner_reference = $"Payroll owner {label} {revision + 1}",
            });
            Assert.Equal(HttpStatusCode.NoContent, revised.StatusCode);
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
                "application", applicationId, label);
            instanceBoundaries[index] = await CreateBoundaryAsync(owner, boundaryPath,
                "system_instance", instances[0], label);
        }
        await ApproveBoundaryAsync(owner, reviewer, reviewerEmail, delivery,
            tenantId, applicationId, applicationBoundaries[0]);
        await WaitForItemsAsync(owner, applicationPath + "/boundary-references", 2);
        await WaitForItemsAsync(owner, applicationPath + "/system-instances/" + instances[0] +
            "/boundary-references", 2);
        _ = await WaitForOkAsync(() => owner.PostAsJsonAsync(
            applicationPath + "/change-previews", new
            {
                expected_application_revision = 3,
                change_kind = "retire",
            }));
        return new Seed(label, tenantId, applicationId, instances, applicationBoundaries,
            instanceBoundaries);
    }

    static async Task ApproveBoundaryAsync(HttpClient owner, HttpClient reviewer,
        string reviewerEmail, MockTenantInvitationDelivery delivery, Guid tenantId,
        Guid applicationId, Guid boundaryId)
    {
        var tenantPath = $"/api/v1/tenants/{tenantId}";
        using (var invitation = await owner.PostAsJsonAsync(tenantPath + "/invitations",
                   new { email_address = reviewerEmail, affiliation = "client_personnel", administrator = false }))
            Assert.Equal(HttpStatusCode.NoContent, invitation.StatusCode);
        string? token = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline &&
               !delivery.TryGetLatest(Uuid.Parse(tenantId.ToString(), CultureInfo.InvariantCulture),
                   reviewerEmail, out token))
            await Task.Delay(250);
        Assert.NotNull(token);
        using (var accepted = await reviewer.PostAsJsonAsync(tenantPath + "/invitations/acceptance",
                   new { email_address = reviewerEmail, token }))
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        using (var session = await reviewer.GetAsync("/auth/session"))
        {
            Assert.Equal(HttpStatusCode.OK, session.StatusCode);
            var reviewerId = Uuid.Parse((await ReadAsync(session)).GetProperty("id").GetString()!,
                CultureInfo.InvariantCulture);
            var tenant = Uuid.Parse(tenantId.ToString(), CultureInfo.InvariantCulture);
            var memberId = RbacIds.Member(tenant, reviewerId);
            using var assigned = await owner.PostAsync(tenantPath + "/teams/" +
                BuiltInRbac.PowerUsersTeamId(tenant) + "/members/" + memberId, null);
            Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);
        }

        var boundaryPath = tenantPath + "/boundaries/" + boundaryId;
        var boundary = await WaitForOkAsync(() => owner.GetAsync(boundaryPath));
        var versionId = boundary.GetProperty("draft").GetProperty("version_id").GetString()!;
        var preview = await WaitForOkAsync(() => owner.GetAsync(boundaryPath + "/drafts/" +
            versionId + "/impact-preview?expected_revision=1"));
        string? reviewId = null;
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var review = await reviewer.PostAsJsonAsync(boundaryPath + "/drafts/" +
                versionId + "/reviews", new
                {
                    expected_revision = 1,
                    outcome = "accept",
                    rationale = "A separate tenant member reviewed this application reference.",
                });
            if (review.StatusCode == HttpStatusCode.NoContent)
                break;
            Assert.True(review.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
                await review.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        var reviewed = await WaitForAsync(owner, boundaryPath, result =>
            result.GetProperty("latest_decision").ValueKind == JsonValueKind.Object &&
            result.GetProperty("latest_decision").GetProperty("outcome").GetString() == "accept");
        reviewId = reviewed.GetProperty("latest_decision").GetProperty("decision_id").GetString();
        using (var approved = await reviewer.PostAsJsonAsync(boundaryPath + "/drafts/" +
                   versionId + "/approvals", new
                   {
                       expected_revision = 1,
                       accepted_review_decision_id = reviewId,
                       effective_from = "2027-01-01",
                       rationale = "Approved for application reference isolation proof.",
                       impact_digest = preview.GetProperty("digest").GetString(),
                   }))
            Assert.Equal(HttpStatusCode.NoContent, approved.StatusCode);
        var referencesPath = $"/api/v1/tenants/{tenantId}/applications/{applicationId}/boundary-references";
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var references = await owner.GetAsync(referencesPath);
            if (references.StatusCode == HttpStatusCode.OK)
            {
                var rows = (await ReadAsync(references)).GetProperty("items").EnumerateArray();
                if (rows.Any(item => item.GetProperty("boundary_id").GetString() == boundaryId.ToString() &&
                                     item.GetProperty("status").GetString() == "approved"))
                    return;
            }
            else
                Assert.Equal(HttpStatusCode.Conflict, references.StatusCode);
            await Task.Delay(250);
        }
        throw new TimeoutException("The approved application boundary reference was not projected.");
    }

    static async Task<Guid> AddApplicationAsync(HttpClient owner, Seed seed, string label)
    {
        var path = $"/api/v1/tenants/{seed.TenantId}/applications";
        var applicationId = Guid.Parse((await PostUntilAuthorizedAsync(owner, path,
            new { name = "Payroll " + label, purpose = "Run payroll " + label }))
            .GetProperty("application_id").GetString()!);
        _ = await WaitForOkAsync(() => owner.GetAsync(path + "/" + applicationId));
        return applicationId;
    }

    static async Task AssertApplicationPagesAsync(HttpClient owner, Seed tenant,
        Guid additionalApplication, Guid otherTenantId)
    {
        var path = $"/api/v1/tenants/{tenant.TenantId}/applications";
        var seen = new HashSet<Guid>();
        string? cursor = null;
        var pageCount = 0;
        do
        {
            Assert.True(pageCount++ < 10, "HTTP application list cursor did not terminate.");
            using var response = await owner.GetAsync(path + "?limit=1" + (cursor is null
                ? string.Empty
                : "&cursor=" + Uri.EscapeDataString(cursor)));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var page = await ReadAsync(response);
            var items = page.GetProperty("items").EnumerateArray().ToArray();
            Assert.Single(items);
            foreach (var item in items)
            {
                Assert.Equal(tenant.TenantId.ToString(), item.GetProperty("tenant_id").GetString());
                Assert.Contains(item.GetProperty("application_id").GetString(), new[]
                {
                    tenant.ApplicationId.ToString(), additionalApplication.ToString(),
                }, StringComparer.OrdinalIgnoreCase);
                Assert.True(seen.Add(Guid.Parse(item.GetProperty("application_id").GetString()!)));
            }
            cursor = page.GetProperty("next_cursor").GetString();
        } while (cursor is not null);

        Assert.Equal(new[] { tenant.ApplicationId, additionalApplication }.Order(), seen.Order());
        using var firstPage = await owner.GetAsync(path + "?limit=1");
        var firstPageJson = await ReadAsync(firstPage);
        var foreignPath = $"/api/v1/tenants/{otherTenantId}/applications";
        using var transplanted = await owner.GetAsync(foreignPath + "?limit=1&cursor=" +
            Uri.EscapeDataString(firstPageJson.GetProperty("next_cursor").GetString()!));
        Assert.Equal(HttpStatusCode.BadRequest, transplanted.StatusCode);
    }

    static async Task AssertApplicationMcpPagesAsync(McpScenario mcp, Seed tenant,
        Guid additionalApplication, Guid otherTenantId)
    {
        var seen = new HashSet<Guid>();
        string? cursor = null;
        var pageCount = 0;
        do
        {
            Assert.True(pageCount++ < 10, "MCP application list cursor did not terminate.");
            var args = new Dictionary<string, object?>
            {
                ["tenant_id"] = tenant.TenantId,
                ["limit"] = 1,
            };
            if (cursor is not null)
                args["cursor"] = cursor;
            var call = await mcp.When("bdgrz.application.list", args).ExpectSuccess();
            var page = Assert.IsType<JsonElement>(call.StructuredJson)
                .GetProperty("result");
            var items = page.GetProperty("items").EnumerateArray().ToArray();
            Assert.Single(items);
            foreach (var item in items)
            {
                Assert.Equal(tenant.TenantId.ToString(), item.GetProperty("tenant_id").GetString());
                Assert.Contains(item.GetProperty("application_id").GetString(), new[]
                {
                    tenant.ApplicationId.ToString(), additionalApplication.ToString(),
                }, StringComparer.OrdinalIgnoreCase);
                Assert.True(seen.Add(Guid.Parse(item.GetProperty("application_id").GetString()!)));
            }
            cursor = page.GetProperty("next_cursor").GetString();
        } while (cursor is not null);

        Assert.Equal(new[] { tenant.ApplicationId, additionalApplication }.Order(), seen.Order());
        var firstPage = await mcp.When("bdgrz.application.list", new Dictionary<string, object?>
        {
            ["tenant_id"] = tenant.TenantId,
            ["limit"] = 1,
        }).ExpectSuccess();
        var transplantedCursor = Assert.IsType<JsonElement>(firstPage.StructuredJson)
            .GetProperty("result").GetProperty("next_cursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(transplantedCursor));
        _ = await mcp.When("bdgrz.application.list", new Dictionary<string, object?>
        {
            ["tenant_id"] = otherTenantId,
            ["limit"] = 1,
            ["cursor"] = transplantedCursor,
        }).ExpectFailure("Validation");
    }

    static async Task<Guid> CreateBoundaryAsync(HttpClient owner, string path,
        string subjectType, Guid governedRecordId, string label)
    {
        using var response = await owner.PostAsJsonAsync(path, new
        {
            content = new
            {
                statement = $"Application matrix boundary {label}.",
                engagement_stage = "readiness",
                trust_services_categories = SecurityCategory,
                entries = new[]
                {
                    new
                    {
                        entry_id = Guid.NewGuid(),
                        kind = "inclusion",
                        subject_type = subjectType,
                        subject = $"Payroll {label}",
                        governed_record_id = governedRecordId,
                        owner_reference = "Operations",
                        rationale = $"Declared in scope for tenant {label}.",
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
                ["1", "2", "3"], seed.Label, seed.TenantId, seed.ApplicationId,
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = seed.TenantId,
                    ["application_id"] = seed.ApplicationId,
                    ["minimum_application_revision"] = 3,
                }, "minimum_application_revision=3"),
            new(applicationPath + "/system-instances", "bdgrz.system_instance.list",
                "system_instance_id", seed.Instances.Select(id => id.ToString()).ToArray(),
                seed.Label, seed.TenantId, seed.ApplicationId, new Dictionary<string, object?>
                {
                    ["tenant_id"] = seed.TenantId,
                    ["application_id"] = seed.ApplicationId,
                    ["minimum_application_revision"] = 3,
                }, "minimum_application_revision=3"),
            new(applicationPath + "/boundary-references",
                "bdgrz.application.boundary_references.list", "boundary_id",
                seed.ApplicationBoundaries.Select(id => id.ToString()).ToArray(),
                seed.Label, seed.TenantId, seed.ApplicationId, new Dictionary<string, object?>
                {
                    ["tenant_id"] = seed.TenantId,
                    ["application_id"] = seed.ApplicationId,
                }, ExpectedApprovedBoundaryId: seed.ApplicationBoundaries[0]),
            new(applicationPath + "/system-instances/" + seed.Instances[0] +
                "/boundary-references", "bdgrz.system_instance.boundary_references.list",
                "boundary_id", seed.InstanceBoundaries.Select(id => id.ToString()).ToArray(),
                seed.Label, seed.TenantId, seed.Instances[0], new Dictionary<string, object?>
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
            if (spec.Tool == "bdgrz.application.revision.list")
            {
                Assert.Equal($"Payroll {spec.Label}", item.GetProperty("name").GetString());
                Assert.Equal($"Run payroll {spec.Label}", item.GetProperty("purpose").GetString());
            }
            else if (spec.Tool == "bdgrz.system_instance.list")
            {
                Assert.StartsWith($"Payroll {spec.Label} instance ",
                    item.GetProperty("name").GetString());
                Assert.StartsWith($"payroll-{spec.Label}-",
                    item.GetProperty("source_identifier").GetString());
            }
            else
            {
                Assert.Equal($"Payroll {spec.Label}", item.GetProperty("subject").GetString());
                Assert.Equal($"Declared in scope for tenant {spec.Label}.",
                    item.GetProperty("rationale").GetString());
                if (spec.ExpectedApprovedBoundaryId is { } approvedBoundaryId &&
                    item.GetProperty("boundary_id").GetString() == approvedBoundaryId.ToString())
                {
                    Assert.Equal("approved", item.GetProperty("status").GetString());
                    Assert.Equal("2027-01-01", item.GetProperty("effective_from").GetString());
                }
                else if (spec.Tool == "bdgrz.application.boundary_references.list")
                    Assert.Equal("draft", item.GetProperty("status").GetString());
            }
        }
    }

    static void AssertExactBelongsTo(JsonElement view, Seed seed,
        string idProperty, Guid expectedId)
    {
        Assert.Equal(seed.TenantId.ToString(), view.GetProperty("tenant_id").GetString());
        Assert.Equal(expectedId.ToString(), view.GetProperty(idProperty).GetString());
        Assert.Equal(seed.ApplicationId.ToString(),
            view.GetProperty("application_id").GetString());
        if (idProperty == "application_id")
        {
            Assert.Equal($"Payroll {seed.Label}", view.GetProperty("name").GetString());
            Assert.Equal($"Run payroll {seed.Label}", view.GetProperty("purpose").GetString());
        }
        else
        {
            Assert.Equal($"Payroll {seed.Label} instance 0",
                view.GetProperty("name").GetString());
            Assert.Equal($"payroll-{seed.Label}-0",
                view.GetProperty("source_identifier").GetString());
        }
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
            Assert.Equal($"Payroll {seed.Label}", item.GetProperty("subject").GetString());
            Assert.Equal($"Declared in scope for tenant {seed.Label}.",
                item.GetProperty("rationale").GetString());
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
        throw new TimeoutException($"GET {path} did not reach the expected state.");
    }

    static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    sealed record Seed(string Label, Guid TenantId, Guid ApplicationId, Guid[] Instances,
        Guid[] ApplicationBoundaries, Guid[] InstanceBoundaries);

    sealed record ReadSpec(string Path, string Tool, string IdProperty, string[] ExpectedIds,
        string Label, Guid TenantId, Guid ApplicationOrSubjectId, Dictionary<string, object?> Args,
        string? MinimumRevisionQuery = null, Guid? ExpectedApprovedBoundaryId = null);
}
