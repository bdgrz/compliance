using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Boundaries;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(ApplicationInventoryBrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ApplicationInventoryE2ETests(BrokerStackFixture broker)
{
    [Fact]
    public async Task ShouldProjectDeclarationsGivenSplitApiAndRestartedWorker()
    {
        // Arrange
        var applicationName = $"compliance-split-applications-{Guid.NewGuid():N}";
        using var workerLogs = new AuthorizationDenialLogE2ETests.CapturingLoggerProvider();
        using var worker = BuildWorker(applicationName, workerLogs);
        await worker.StartAsync();
        await using var factory = E2EAppFactory.Create(broker, applicationName);
        var previousMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
        HttpClient client;
        try
        {
            Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", "api");
            client = factory.CreateClient();
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", previousMode);
        }
        using var owner = client;
        await TenantInvitationE2ETests.LoginAsync(owner,
            $"split-app-owner-{Guid.NewGuid():N}@example.com");
        using var tenantResponse = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Split application tenant",
            slug = $"split-app-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, tenantResponse.StatusCode);
        var tenant = await tenantResponse.Content.ReadFromJsonAsync<TenantDocument>();
        Assert.NotNull(tenant);
        var applicationsPath = $"/api/v1/tenants/{tenant.TenantId}/applications";
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);

        // Act
        ApplicationRegistrationDocument? first = null;
        HttpStatusCode? lastStatus = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(applicationsPath, new
            {
                name = "Payroll",
                purpose = "Run payroll",
            });
            if (response.StatusCode == HttpStatusCode.OK)
            {
                first = await response.Content.ReadFromJsonAsync<ApplicationRegistrationDocument>();
                break;
            }
            lastStatus = response.StatusCode;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        Assert.True(first is not null,
            $"Split-host application declaration never became authorized; last status: {lastStatus}.");
        Assert.NotNull(first);
        var firstProjected = false;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync($"{applicationsPath}/{first.ApplicationId}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                firstProjected = true;
                break;
            }
            await Task.Delay(250);
        }
        Assert.True(firstProjected);
        var programId = await CreateProgramAsync(owner, tenant.TenantId);
        var boundaryPath = $"/api/v1/tenants/{tenant.TenantId}/programs/{programId}/boundaries";
        var controlsPath = $"/api/v1/tenants/{tenant.TenantId}/programs/{programId}/controls";
        var applicationReferencesPath =
            $"{applicationsPath}/{first.ApplicationId}/boundary-references";
        await worker.StopAsync();
        using (var emptyInstances = await owner.GetAsync(
                   $"{applicationsPath}/{first.ApplicationId}/system-instances?minimum_application_revision=1"))
        {
            Assert.Equal(HttpStatusCode.OK, emptyInstances.StatusCode);
            var emptyPage = await emptyInstances.Content
                .ReadFromJsonAsync<SystemInstancePageDocument>();
            Assert.Empty(emptyPage!.Items);
        }
        using var applicationBoundaryResponse = await owner.PostAsJsonAsync(boundaryPath,
            new { content = BoundaryContent("application", first.ApplicationId) });
        Assert.Equal(HttpStatusCode.OK, applicationBoundaryResponse.StatusCode);
        var applicationBoundary = await applicationBoundaryResponse.Content
            .ReadFromJsonAsync<BoundaryRegistrationDocument>();
        Assert.NotNull(applicationBoundary);
        var controlEntryId = Guid.NewGuid();
        using var applicationControlResponse = await owner.PostAsJsonAsync(controlsPath, new
        {
            identifier = "AC-APP-PREVIEW",
            content = ControlContent(first.ApplicationId, controlEntryId),
        });
        Assert.Equal(HttpStatusCode.OK, applicationControlResponse.StatusCode);
        var applicationControl = await applicationControlResponse.Content
            .ReadFromJsonAsync<ControlRegistrationDocument>();
        Assert.NotNull(applicationControl);
        var previewPath = $"{applicationsPath}/{first.ApplicationId}/change-previews";
        using (var pendingPreview = await owner.PostAsJsonAsync(previewPath, new
        {
            expected_application_revision = 1,
            change_kind = "retire",
        }))
        {
            Assert.Equal(HttpStatusCode.Conflict, pendingPreview.StatusCode);
            Assert.Equal("true", pendingPreview.Headers.GetValues("Portia-Transient").Single());
        }
        using (var pendingReferences = await owner.GetAsync(applicationReferencesPath))
        {
            Assert.Equal(HttpStatusCode.Conflict, pendingReferences.StatusCode);
            Assert.Equal("true", pendingReferences.Headers.GetValues("Portia-Transient").Single());
        }
        await using (var pendingMcp = await McpScenario.ConnectAsync(owner,
                         new Uri(owner.BaseAddress!, "/mcp")))
        {
            var pendingReferences = await pendingMcp.When(
                "bdgrz.application.boundary_references.list", new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenant.TenantId,
                    ["application_id"] = first.ApplicationId,
                }).ExpectFailure("Conflict");
            var structured = Assert.IsType<JsonElement>(pendingReferences.Error);
            Assert.True(structured.GetProperty("isTransient").GetBoolean());
        }
        using var unprojectedInstanceResponse = await owner.PostAsJsonAsync(
            $"{applicationsPath}/{first.ApplicationId}/system-instances", new
            {
                expected_application_revision = 1,
                name = "Payroll production",
                kind = "production",
                source_identifier = "payroll-prod",
            });
        Assert.Equal(HttpStatusCode.OK, unprojectedInstanceResponse.StatusCode);
        var unprojectedInstance = await unprojectedInstanceResponse.Content
            .ReadFromJsonAsync<SystemInstanceRegistrationDocument>();
        Assert.NotNull(unprojectedInstance);
        var instancePath = $"{applicationsPath}/{first.ApplicationId}/system-instances/" +
            unprojectedInstance.SystemInstanceId;
        using (var pendingExact = await owner.GetAsync(
                   $"{instancePath}?minimum_application_revision=1&minimum_instance_revision=1"))
        using (var pendingList = await owner.GetAsync(
                   $"{applicationsPath}/{first.ApplicationId}/system-instances?minimum_application_revision=1"))
        using (var future = await owner.GetAsync(
                   $"{instancePath}?minimum_instance_revision=2"))
        using (var unknown = await owner.GetAsync(
                   $"{applicationsPath}/{first.ApplicationId}/system-instances/{Guid.NewGuid()}"))
        using (var invalid = await owner.GetAsync(
                   $"{instancePath}?minimum_instance_revision=0"))
        {
            Assert.Equal(HttpStatusCode.Conflict, pendingExact.StatusCode);
            Assert.Equal("true", pendingExact.Headers.GetValues("Portia-Transient").Single());
            Assert.Equal(HttpStatusCode.Conflict, pendingList.StatusCode);
            Assert.Equal("true", pendingList.Headers.GetValues("Portia-Transient").Single());
            Assert.Equal(HttpStatusCode.Conflict, future.StatusCode);
            Assert.Contains("source", await future.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        var instanceContent = BoundaryContent("system_instance",
            unprojectedInstance.SystemInstanceId);
        using var laggingReference = await owner.PostAsJsonAsync(boundaryPath,
            new { content = instanceContent });
        Assert.Equal(HttpStatusCode.Conflict, laggingReference.StatusCode);
        using var restarted = BuildWorker(applicationName, workerLogs);
        await restarted.StartAsync();
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        try
        {
            var instanceProjected = false;
            while (DateTimeOffset.UtcNow < deadline)
            {
                using var response = await owner.GetAsync(
                    $"{applicationsPath}/{first.ApplicationId}/system-instances/" +
                    unprojectedInstance.SystemInstanceId);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    instanceProjected = true;
                    break;
                }
                await Task.Delay(250);
            }
            Assert.True(instanceProjected);
            using (var caughtUpList = await owner.GetAsync(
                       $"{applicationsPath}/{first.ApplicationId}/system-instances?minimum_application_revision=1"))
            {
                Assert.Equal(HttpStatusCode.OK, caughtUpList.StatusCode);
                var page = await caughtUpList.Content
                    .ReadFromJsonAsync<SystemInstancePageDocument>();
                Assert.Equal(unprojectedInstance.SystemInstanceId,
                    Assert.Single(page!.Items).SystemInstanceId);
            }
            using var diagnosticScope = factory.Services.CreateScope();
            var boundaryDirectory = diagnosticScope.ServiceProvider
                .GetRequiredService<IBoundaryDirectoryReader>();
            var parsedTenantId = Uuid.Parse(tenant.TenantId.ToString(),
                CultureInfo.InvariantCulture);
            var boundaryCheckpointBefore = await boundaryDirectory
                .LoadCheckpointAsync(parsedTenantId);
            using var linked = await owner.PostAsJsonAsync(boundaryPath,
                new { content = instanceContent });
            Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
            var linkedBoundary = await linked.Content
                .ReadFromJsonAsync<BoundaryRegistrationDocument>();
            Assert.NotNull(linkedBoundary);
            using var wrongRecordType = await owner.PostAsJsonAsync(boundaryPath,
                new { content = BoundaryContent("system_instance", first.ApplicationId) });
            using var absentRecord = await owner.PostAsJsonAsync(boundaryPath,
                new { content = BoundaryContent("system_instance", Guid.NewGuid()) });
            Assert.Equal(HttpStatusCode.Conflict, wrongRecordType.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, absentRecord.StatusCode);
            const string missingReference =
                "The governed system instance reference is unavailable in this tenant.";
            Assert.Contains(missingReference,
                await wrongRecordType.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            Assert.Contains(missingReference,
                await absentRecord.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            deadline = DateTimeOffset.UtcNow.AddSeconds(45);
            var boundaryProjected = false;
            HttpStatusCode? lastBoundaryStatus = null;
            string? lastBoundaryBody = null;
            var linkedBoundaryPath =
                $"/api/v1/tenants/{tenant.TenantId}/boundaries/{linkedBoundary.BoundaryId}";
            while (DateTimeOffset.UtcNow < deadline)
            {
                using var response = await owner.GetAsync(linkedBoundaryPath);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    boundaryProjected = true;
                    break;
                }
                lastBoundaryStatus = response.StatusCode;
                lastBoundaryBody = response.StatusCode is HttpStatusCode.NotFound or
                    HttpStatusCode.Conflict
                    ? await response.Content.ReadAsStringAsync()
                    : "<unexpected status body omitted>";
                await Task.Delay(250);
            }
            if (!boundaryProjected)
            {
                var boundaryCheckpointAfter = await boundaryDirectory
                    .LoadCheckpointAsync(parsedTenantId);
                var originalBoundaryId = Uuid.Parse(applicationBoundary.BoundaryId.ToString(),
                    CultureInfo.InvariantCulture);
                var originalBoundaryProjected = await boundaryDirectory.GetAsync(parsedTenantId,
                    originalBoundaryId) is not null;
                var parsedBoundaryId = Uuid.Parse(linkedBoundary.BoundaryId.ToString(),
                    CultureInfo.InvariantCulture);
                var boundarySource = await diagnosticScope.ServiceProvider
                    .GetRequiredService<IAggregateReader>()
                    .HydrateAsync(new SystemBoundary(parsedTenantId, parsedBoundaryId));
                using var minimumRead = await owner.GetAsync(
                    $"{linkedBoundaryPath}?minimum_revision=1");
                var minimumBody = minimumRead.StatusCode is HttpStatusCode.NotFound or
                    HttpStatusCode.Conflict
                    ? await minimumRead.Content.ReadAsStringAsync()
                    : "<unexpected status body omitted>";
                var faults = workerLogs.Records
                    .Where(record => record.Level >= LogLevel.Error &&
                                     (record.Category.StartsWith("Cntryl.Portia", StringComparison.Ordinal) ||
                                      record.Category.StartsWith("Microsoft.Extensions.Hosting",
                                          StringComparison.Ordinal)))
                    .ToArray();
                var workerFaults = string.Join(", ", faults.Take(10)
                    .Select(record =>
                        $"{record.Category}/{record.EventName}/{record.Value("RunnerName")}/" +
                        $"{record.Value("Stage")}/{record.Value("ErrorType")}"));
                Assert.Fail($"Boundary projection did not catch up in 45 seconds. " +
                            $"Last GET: {(int?)lastBoundaryStatus} {lastBoundaryBody}; " +
                            $"source created: {boundarySource.IsCreated}, " +
                            $"source revision: {boundarySource.Revision}; " +
                            $"minimum-revision GET: {(int)minimumRead.StatusCode} {minimumBody}; " +
                            $"earlier boundary projected: {originalBoundaryProjected}; " +
                            $"projection checkpoint initially at start: " +
                            $"{boundaryCheckpointBefore == ProjectionCheckpoint.Start}, " +
                            $"checkpoint changed after pre-write sample: " +
                            $"{boundaryCheckpointAfter != boundaryCheckpointBefore}; " +
                            $"worker fault count: {faults.Length}, first faults: [{workerFaults}].");
            }
            var instanceReferencesPath =
                $"{applicationsPath}/{first.ApplicationId}/system-instances/" +
                $"{unprojectedInstance.SystemInstanceId}/boundary-references";
            deadline = DateTimeOffset.UtcNow.AddSeconds(45);
            var instanceReferenceProjected = false;
            while (DateTimeOffset.UtcNow < deadline)
            {
                using var response = await owner.GetAsync(instanceReferencesPath);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    instanceReferenceProjected = body.Contains(
                        linkedBoundary.BoundaryId.ToString(), StringComparison.OrdinalIgnoreCase);
                    if (instanceReferenceProjected)
                        break;
                }
                else
                    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                await Task.Delay(250);
            }
            Assert.True(instanceReferenceProjected);
            using (var invalidApplicationLimit = await owner.GetAsync(
                       $"{applicationReferencesPath}?limit=0"))
            using (var oversizedApplicationLimit = await owner.GetAsync(
                       $"{applicationReferencesPath}?limit=201"))
            using (var invalidApplicationCursor = await owner.GetAsync(
                       $"{applicationReferencesPath}?cursor=not-a-cursor"))
            using (var invalidInstanceLimit = await owner.GetAsync(
                       $"{instanceReferencesPath}?limit=0"))
            using (var oversizedInstanceLimit = await owner.GetAsync(
                       $"{instanceReferencesPath}?limit=201"))
            using (var invalidInstanceCursor = await owner.GetAsync(
                       $"{instanceReferencesPath}?cursor=not-a-cursor"))
            {
                Assert.Equal(HttpStatusCode.BadRequest, invalidApplicationLimit.StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, oversizedApplicationLimit.StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, invalidApplicationCursor.StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, invalidInstanceLimit.StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, oversizedInstanceLimit.StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, invalidInstanceCursor.StatusCode);
            }
            using (var applicationReferences = await owner.GetAsync(applicationReferencesPath))
            {
                Assert.Equal(HttpStatusCode.OK, applicationReferences.StatusCode);
                using var document = JsonDocument.Parse(
                    await applicationReferences.Content.ReadAsStringAsync());
                var reference = Assert.Single(document.RootElement.GetProperty("items")
                    .EnumerateArray());
                Assert.Equal(applicationBoundary.BoundaryId.ToString(),
                    reference.GetProperty("boundary_id").GetString());
            }
            string? previewJson = null;
            HttpStatusCode? lastPreviewStatus = null;
            deadline = DateTimeOffset.UtcNow.AddSeconds(45);
            while (DateTimeOffset.UtcNow < deadline)
            {
                using var previewResponse = await owner.PostAsJsonAsync(previewPath, new
                {
                    expected_application_revision = 1,
                    change_kind = "revise",
                    name = "Payroll revised",
                    purpose = "Run payroll",
                });
                if (previewResponse.StatusCode == HttpStatusCode.OK)
                {
                    previewJson = await previewResponse.Content.ReadAsStringAsync();
                    break;
                }
                lastPreviewStatus = previewResponse.StatusCode;
                Assert.Equal(HttpStatusCode.Conflict, previewResponse.StatusCode);
                Assert.Equal("true", previewResponse.Headers.GetValues("Portia-Transient").Single());
                await Task.Delay(250);
            }
            Assert.True(previewJson is not null,
                $"Application preview never reached the Control reference projection; last status: {lastPreviewStatus}.");
            using (var preview = JsonDocument.Parse(previewJson))
            {
                Assert.False(preview.RootElement.GetProperty("complete").GetBoolean());
                Assert.Equal("name", Assert.Single(preview.RootElement
                    .GetProperty("changes").EnumerateArray()).GetProperty("field").GetString());
                Assert.Equal(applicationBoundary.BoundaryId.ToString(),
                    Assert.Single(preview.RootElement.GetProperty("boundary_references")
                        .EnumerateArray()).GetProperty("boundary_id").GetString());
                var controlReference = Assert.Single(preview.RootElement
                    .GetProperty("control_draft_references").EnumerateArray());
                Assert.Equal(applicationControl.ControlId.ToString(),
                    controlReference.GetProperty("control_id").GetString());
                Assert.Equal(programId.ToString(), controlReference.GetProperty("program_id").GetString());
                Assert.Equal("AC-APP-PREVIEW", controlReference.GetProperty("identifier").GetString());
                Assert.Equal(1, controlReference.GetProperty("revision").GetInt64());
                Assert.Equal(controlEntryId.ToString(), controlReference.GetProperty("entry_id").GetString());
                Assert.Contains(preview.RootElement.GetProperty("pending_contexts")
                    .EnumerateArray(), item => item.GetString() ==
                        "approved_control_versions_and_lifecycle_impact");
                Assert.DoesNotContain(preview.RootElement.GetProperty("pending_contexts")
                    .EnumerateArray(), item => item.GetString() == "controls");
                Assert.Contains(preview.RootElement.GetProperty("pending_contexts")
                    .EnumerateArray(), item => item.GetString() == "engagements");
            }
            await using (var mcp = await McpScenario.ConnectAsync(owner,
                             new Uri(owner.BaseAddress!, "/mcp")))
            {
                var previewCall = await mcp.When("bdgrz.application.change.preview",
                    new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenant.TenantId,
                        ["application_id"] = first.ApplicationId,
                        ["expected_application_revision"] = 1,
                        ["change_kind"] = "retire",
                    }).ExpectSuccess();
                var preview = Assert.IsType<JsonElement>(previewCall.StructuredJson)
                    .GetProperty("result");
                var controlReference = Assert.Single(preview.GetProperty("control_draft_references")
                    .EnumerateArray());
                Assert.Equal(applicationControl.ControlId.ToString(),
                    controlReference.GetProperty("control_id").GetString());
                Assert.Contains(preview.GetProperty("pending_contexts").EnumerateArray(),
                    item => item.GetString() == "approved_control_versions_and_lifecycle_impact");
                _ = await mcp.When("bdgrz.boundary.get", new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenant.TenantId,
                    ["boundary_id"] = linkedBoundary.BoundaryId,
                }).ExpectSuccess();
                _ = await mcp.When("bdgrz.application.boundary_references.list",
                    new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenant.TenantId,
                        ["application_id"] = first.ApplicationId,
                    }).ExpectSuccess();
                _ = await mcp.When("bdgrz.application.boundary_references.list",
                    new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenant.TenantId,
                        ["application_id"] = first.ApplicationId,
                        ["limit"] = 0,
                    }).ExpectFailure();
                _ = await mcp.When("bdgrz.system_instance.boundary_references.list",
                    new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenant.TenantId,
                        ["application_id"] = first.ApplicationId,
                        ["system_instance_id"] = unprojectedInstance.SystemInstanceId,
                    }).ExpectSuccess();
                _ = await mcp.When("bdgrz.system_instance.boundary_references.list",
                    new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenant.TenantId,
                        ["application_id"] = first.ApplicationId,
                        ["system_instance_id"] = unprojectedInstance.SystemInstanceId,
                        ["cursor"] = "not-a-cursor",
                    }).ExpectFailure();
            }
            using var secondResponse = await owner.PostAsJsonAsync(applicationsPath, new
            {
                name = "Benefits",
                purpose = "Administer benefits",
            });
            Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
            var second = await secondResponse.Content
                .ReadFromJsonAsync<ApplicationRegistrationDocument>();
            Assert.NotNull(second);
            ApplicationDocument? projected = null;
            while (DateTimeOffset.UtcNow < deadline)
            {
                using var response = await owner.GetAsync($"{applicationsPath}/{second.ApplicationId}");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    projected = await response.Content.ReadFromJsonAsync<ApplicationDocument>();
                    break;
                }
                await Task.Delay(250);
            }

            // Assert
            Assert.Equal(1, projected?.Revision);
            using var persisted = await owner.GetAsync($"{applicationsPath}/{first.ApplicationId}");
            Assert.Equal(HttpStatusCode.OK, persisted.StatusCode);
            using var instanceResponse = await owner.PostAsJsonAsync(
                $"{applicationsPath}/{second.ApplicationId}/system-instances", new
                {
                    expected_application_revision = 1,
                    name = "Benefits production",
                    kind = "production",
                    source_identifier = "benefits-prod",
                });
            Assert.Equal(HttpStatusCode.OK, instanceResponse.StatusCode);
            var instance = await instanceResponse.Content
                .ReadFromJsonAsync<SystemInstanceRegistrationDocument>();
            Assert.NotNull(instance);
            SystemInstanceDocument? projectedInstance = null;
            deadline = DateTimeOffset.UtcNow.AddSeconds(45);
            while (DateTimeOffset.UtcNow < deadline)
            {
                using var response = await owner.GetAsync(
                    $"{applicationsPath}/{second.ApplicationId}/system-instances/{instance.SystemInstanceId}");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    projectedInstance = await response.Content.ReadFromJsonAsync<SystemInstanceDocument>();
                    break;
                }
                await Task.Delay(250);
            }
            Assert.Equal("benefits-prod", projectedInstance?.SourceIdentifier);
            Assert.Equal(1, projectedInstance?.Revision);
            Assert.Null(projectedInstance?.LegacyApplicationRevision);
            using var unchangedApplication = await owner.GetAsync(
                $"{applicationsPath}/{second.ApplicationId}");
            Assert.Equal(HttpStatusCode.OK, unchangedApplication.StatusCode);
            Assert.Equal(1, (await unchangedApplication.Content
                .ReadFromJsonAsync<ApplicationDocument>())?.Revision);
            using var noInstanceMetadataRevision = await owner.GetAsync(
                $"{applicationsPath}/{second.ApplicationId}/revisions/2");
            Assert.Equal(HttpStatusCode.Conflict, noInstanceMetadataRevision.StatusCode);
            using var wrongApplication = await owner.GetAsync(
                $"{applicationsPath}/{first.ApplicationId}/system-instances/{instance.SystemInstanceId}");
            Assert.Equal(HttpStatusCode.NotFound, wrongApplication.StatusCode);
            using var firstPageResponse = await owner.GetAsync($"{applicationsPath}?limit=1");
            Assert.Equal(HttpStatusCode.OK, firstPageResponse.StatusCode);
            var firstPage = await firstPageResponse.Content.ReadFromJsonAsync<ApplicationPageDocument>();
            Assert.NotNull(firstPage);
            Assert.Single(firstPage.Items);
            Assert.NotNull(firstPage.NextCursor);
            using var secondPageResponse = await owner.GetAsync(
                $"{applicationsPath}?limit=1&cursor={Uri.EscapeDataString(firstPage.NextCursor)}");
            Assert.Equal(HttpStatusCode.OK, secondPageResponse.StatusCode);
            var secondPage = await secondPageResponse.Content.ReadFromJsonAsync<ApplicationPageDocument>();
            Assert.NotNull(secondPage);
            Assert.Single(secondPage.Items);
            Assert.NotEqual(firstPage.Items[0].ApplicationId, secondPage.Items[0].ApplicationId);
        }
        finally
        {
            await restarted.StopAsync();
        }
    }

    IHost BuildWorker(string applicationName, ILoggerProvider workerLogs)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
        builder.Logging.AddProvider(workerLogs);
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
        return builder.Build();
    }

    static async Task<Guid> CreateProgramAsync(HttpClient owner, Guid tenantId)
    {
        var path = $"/api/v1/tenants/{tenantId}/programs";
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(path, new
            {
                name = "Application boundary program",
                plan = new
                {
                    target_readiness_date = "2027-01-31",
                    target_type_i_as_of_date = "2027-03-31",
                    target_type_ii_start_date = "2027-04-01",
                    target_type_ii_end_date = "2028-03-31",
                    readiness_advisor = "Advisor",
                    audit_firm = (string?)null,
                },
            });
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var program = await response.Content
                    .ReadFromJsonAsync<ProgramRegistrationDocument>();
                Assert.NotNull(program);
                return program.ProgramId;
            }
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("Program creation remained unauthorized after bootstrap.");
    }

    static object BoundaryContent(string subjectType, Guid governedRecordId) => new
    {
        statement = "Declared application inventory boundary.",
        engagement_stage = "readiness",
        trust_services_categories = new[] { "security" },
        entries = new[]
        {
            new
            {
                entry_id = Guid.NewGuid(),
                kind = "inclusion",
                subject_type = subjectType,
                subject = "Payroll production",
                governed_record_id = governedRecordId,
                owner_reference = "Operations",
                rationale = "Declared as in scope; ownership and classification remain unresolved.",
                unresolved = false,
            },
        },
    };

    static object ControlContent(Guid applicationId, Guid entryId) => new
    {
        title = "Review application access",
        objective = "Ensure application access is reviewed",
        description = "Payroll access is reviewed before changes are approved.",
        implementation_narrative = "The compliance lead reviews the access listing.",
        expected_evidence_descriptions = new[] { "Access review record" },
        applicability = new[]
        {
            new
            {
                entry_id = entryId,
                subject_type = "application",
                subject = "Payroll",
                governed_record_id = applicationId,
                rationale = "The draft applies to the declared payroll application.",
                unresolved = false,
            },
        },
    };

    [Fact]
    public async Task ShouldDeclareApplicationAndSystemInstanceGivenAuthorizedBrokerHost()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var owner = factory.CreateClient();
        using var outsider = factory.CreateClient();
        await TenantInvitationE2ETests.LoginAsync(owner,
            $"app-owner-{Guid.NewGuid():N}@example.com");
        await TenantInvitationE2ETests.LoginAsync(outsider,
            $"app-outsider-{Guid.NewGuid():N}@example.com");
        using var tenantResponse = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Application tenant",
            slug = $"app-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, tenantResponse.StatusCode);
        var tenant = await tenantResponse.Content.ReadFromJsonAsync<TenantDocument>();
        Assert.NotNull(tenant);
        var applicationsPath = $"/api/v1/tenants/{tenant.TenantId}/applications";
        var peoplePath = $"/api/v1/tenants/{tenant.TenantId}/people";
        var systemOwnerId = await RecordPersonAsync(owner, peoplePath, "System Owner");
        var accessOwnerId = await RecordPersonAsync(owner, peoplePath, "Access Owner");
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);

        // Act
        ApplicationRegistrationDocument? registration = null;
        HttpStatusCode? lastStatus = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(applicationsPath, new
            {
                name = "Payroll",
                purpose = "Run payroll",
                owner_reference = "Finance",
                classification = "internal",
                system_owner_person_id = systemOwnerId,
                access_owner_person_id = accessOwnerId,
            });
            if (response.StatusCode == HttpStatusCode.OK)
            {
                registration = await response.Content
                    .ReadFromJsonAsync<ApplicationRegistrationDocument>();
                break;
            }
            lastStatus = response.StatusCode;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        Assert.True(registration is not null,
            $"Application declaration never became authorized; last status: {lastStatus}.");
        Assert.NotNull(registration);
        var applicationPath = $"{applicationsPath}/{registration.ApplicationId}";
        ApplicationDocument? application = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(applicationPath);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                application = await response.Content.ReadFromJsonAsync<ApplicationDocument>();
                if (application?.Revision == 1)
                    break;
            }
            await Task.Delay(250);
        }

        // Assert
        Assert.NotNull(application);
        Assert.Equal(1, application.Revision);
        Assert.Equal("manual", application.SourceKind);
        Assert.Equal("internal", application.Classification);
        Assert.Equal(systemOwnerId, application.SystemOwnerPersonId);
        Assert.Equal(accessOwnerId, application.AccessOwnerPersonId);
        Assert.Contains("classification_unverified", application.Unresolved);
        Assert.Contains("owner_unverified", application.Unresolved);
        using (var zeroLimit = await owner.GetAsync($"{applicationsPath}?limit=0"))
        using (var oversizedLimit = await owner.GetAsync($"{applicationsPath}?limit=201"))
        using (var malformedCursor = await owner.GetAsync(
                   $"{applicationsPath}?cursor=not-a-cursor"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, zeroLimit.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, oversizedLimit.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, malformedCursor.StatusCode);
        }
        using var future = await owner.GetAsync($"{applicationPath}?minimum_revision=2");
        Assert.Equal(HttpStatusCode.Conflict, future.StatusCode);
        using var denied = await outsider.GetAsync(applicationPath);
        using var deniedList = await outsider.GetAsync(applicationsPath);
        using var deniedPreview = await outsider.PostAsJsonAsync(
            $"{applicationPath}/change-previews", new
            {
                expected_application_revision = 1,
                change_kind = "retire",
            });
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedList.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedPreview.StatusCode);
        await using (var mcp = await McpScenario.ConnectAsync(owner,
                         new Uri(owner.BaseAddress!, "/mcp")))
        {
            _ = await mcp.When("bdgrz.application.get", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenant.TenantId,
                ["application_id"] = registration.ApplicationId,
            }).ExpectSuccess();
            _ = await mcp.When("bdgrz.application.declare", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenant.TenantId,
                ["name"] = "Benefits",
                ["purpose"] = "Administer benefits",
                ["classification"] = "internal",
                ["system_owner_person_id"] = systemOwnerId,
                ["access_owner_person_id"] = accessOwnerId,
            }).ExpectSuccess();
            _ = await mcp.When("bdgrz.application.list", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenant.TenantId,
                ["limit"] = 0,
            }).ExpectFailure();
            _ = await mcp.When("bdgrz.application.list", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenant.TenantId,
                ["cursor"] = "not-a-cursor",
            }).ExpectFailure();
        }
        await using (var mcp = await McpScenario.ConnectAsync(outsider,
                         new Uri(outsider.BaseAddress!, "/mcp")))
        {
            _ = await mcp.When("bdgrz.application.revision.get", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenant.TenantId,
                ["application_id"] = registration.ApplicationId,
                ["revision"] = 1,
            }).ExpectFailure();
            _ = await mcp.When("bdgrz.application.revision.list", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenant.TenantId,
                ["application_id"] = registration.ApplicationId,
            }).ExpectFailure();
            _ = await mcp.When("bdgrz.application.boundary_references.list",
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenant.TenantId,
                    ["application_id"] = registration.ApplicationId,
                }).ExpectFailure();
            _ = await mcp.When("bdgrz.application.change.preview",
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenant.TenantId,
                    ["application_id"] = registration.ApplicationId,
                    ["expected_application_revision"] = 1,
                    ["change_kind"] = "retire",
                }).ExpectFailure();
            _ = await mcp.When("bdgrz.system_instance.boundary_references.list",
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenant.TenantId,
                    ["application_id"] = registration.ApplicationId,
                    ["system_instance_id"] = Uuid.CreateVersion4(),
                }).ExpectFailure();
            _ = await mcp.When("bdgrz.system_instance.get", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenant.TenantId,
                ["application_id"] = registration.ApplicationId,
                ["system_instance_id"] = Uuid.CreateVersion4(),
                ["minimum_application_revision"] = 1,
            }).ExpectFailure();
            _ = await mcp.When("bdgrz.system_instance.list", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenant.TenantId,
                ["application_id"] = registration.ApplicationId,
                ["minimum_application_revision"] = 1,
            }).ExpectFailure();
        }
        await using (var mcp = await McpScenario.ConnectAsync(outsider,
                         new Uri(outsider.BaseAddress!, "/mcp")))
        {
            _ = await mcp.When("bdgrz.application.get", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenant.TenantId,
                ["application_id"] = registration.ApplicationId,
            }).ExpectFailure();
            _ = await mcp.When("bdgrz.application.declare", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenant.TenantId,
                ["name"] = "Hidden",
                ["purpose"] = "Should be denied",
            }).ExpectFailure();
        }
        var productionTask = owner.PostAsJsonAsync(
            $"{applicationPath}/system-instances", new
            {
                expected_application_revision = 1,
                name = "Production",
                kind = "production",
                source_identifier = "payroll-prod",
            });
        var stagingTask = owner.PostAsJsonAsync($"{applicationPath}/system-instances", new
        {
            expected_application_revision = 1,
            name = "Staging",
            kind = "staging",
        });
        await Task.WhenAll(productionTask, stagingTask);
        using var instanceResponse = await productionTask;
        using var independent = await stagingTask;
        Assert.Equal(HttpStatusCode.OK, instanceResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, independent.StatusCode);
        var instance = await instanceResponse.Content
            .ReadFromJsonAsync<SystemInstanceRegistrationDocument>();
        Assert.NotNull(instance);
        var independentInstance = await independent.Content
            .ReadFromJsonAsync<SystemInstanceRegistrationDocument>();
        Assert.NotNull(independentInstance);
        Assert.NotEqual(instance.SystemInstanceId, independentInstance.SystemInstanceId);
        var instancePath = $"{applicationPath}/system-instances/{instance.SystemInstanceId}";
        SystemInstanceDocument? projected = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(instancePath);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                projected = await response.Content.ReadFromJsonAsync<SystemInstanceDocument>();
                if (projected is not null)
                    break;
            }
            await Task.Delay(250);
        }
        Assert.Equal("manual", projected?.SourceKind);
        Assert.Contains("access_boundary_missing", projected!.Unresolved);
        Assert.Equal("payroll-prod", projected.SourceIdentifier);
        Assert.Equal(1, projected.Revision);
        Assert.Null(projected.LegacyApplicationRevision);
        Assert.Contains("source_identifier_unverified", projected.Unresolved);
        using var applicationAfterInstances = await owner.GetAsync(applicationPath);
        Assert.Equal(HttpStatusCode.OK, applicationAfterInstances.StatusCode);
        Assert.Equal(1, (await applicationAfterInstances.Content
            .ReadFromJsonAsync<ApplicationDocument>())?.Revision);
        using (var sourceScope = factory.Services.CreateScope())
        {
            var events = sourceScope.ServiceProvider.GetRequiredService<IDomainEventReader>();
            var appEvents = new List<DomainEventRecord>();
            await foreach (var record in events.ReadAsync(new EventStreamAddress(
                               tenant.TenantId.ToString(), "applications",
                               registration.ApplicationId.ToString()), 0,
                               CancellationToken.None))
                appEvents.Add(record);
            Assert.IsType<ApplicationDeclared>(Assert.Single(appEvents).Event);
            var instanceEvents = new List<DomainEventRecord>();
            await foreach (var record in events.ReadAsync(new EventStreamAddress(
                               tenant.TenantId.ToString(), "system-instances",
                               instance.SystemInstanceId.ToString()), 0,
                               CancellationToken.None))
                instanceEvents.Add(record);
            var registered = Assert.IsType<SystemInstanceRegistered>(
                Assert.Single(instanceEvents).Event);
            Assert.Equal(registration.ApplicationId.ToString(),
                registered.ApplicationId.ToString());
            Assert.Equal(1, registered.Revision);
        }
        using var deniedApplicationReferences = await outsider.GetAsync(
            $"{applicationPath}/boundary-references");
        using var deniedInstanceReferences = await outsider.GetAsync(
            $"{instancePath}/boundary-references");
        using var wrongParentReferences = await owner.GetAsync(
            $"{applicationsPath}/{Guid.NewGuid()}/system-instances/" +
            $"{instance.SystemInstanceId}/boundary-references");
        Assert.Equal(HttpStatusCode.NotFound, deniedApplicationReferences.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedInstanceReferences.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, wrongParentReferences.StatusCode);
        using var noInstanceMetadataRevision = await owner.GetAsync(
            $"{applicationPath}/revisions/2");
        Assert.Equal(HttpStatusCode.Conflict, noInstanceMetadataRevision.StatusCode);
        using var revisedResponse = await owner.PutAsJsonAsync(applicationPath, new
        {
            expected_revision = 1,
            name = "Payroll",
            purpose = "Run monthly payroll",
            owner_reference = "Finance",
            classification = "restricted",
        });
        Assert.Equal(HttpStatusCode.NoContent, revisedResponse.StatusCode);
        ApplicationRevisionDocument? revisedRevision = null;
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync($"{applicationPath}/revisions/2");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                revisedRevision = await response.Content
                    .ReadFromJsonAsync<ApplicationRevisionDocument>();
                break;
            }
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await Task.Delay(250);
        }
        Assert.Equal("revised", revisedRevision?.ChangeKind);
        Assert.Equal("Run monthly payroll", revisedRevision?.Purpose);
        Assert.Equal("restricted", revisedRevision?.Classification);
        using var afterMetadataEdit = await owner.PostAsJsonAsync(
            $"{applicationPath}/system-instances", new
            {
                expected_application_revision = 1,
                name = "Recovery",
                kind = "recovery",
            });
        Assert.Equal(HttpStatusCode.OK, afterMetadataEdit.StatusCode);
        var recovery = await afterMetadataEdit.Content
            .ReadFromJsonAsync<SystemInstanceRegistrationDocument>();
        Assert.NotNull(recovery);
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        var recoveryProjected = false;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(
                $"{applicationPath}/system-instances/{recovery.SystemInstanceId}" +
                "?minimum_instance_revision=1");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                recoveryProjected = true;
                break;
            }
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await Task.Delay(250);
        }
        Assert.True(recoveryProjected);
        using (var freshInstance = await owner.GetAsync(
                   $"{instancePath}?minimum_application_revision=2&minimum_instance_revision=1"))
        using (var freshInstances = await owner.GetAsync(
                   $"{applicationPath}/system-instances?minimum_application_revision=2"))
        {
            Assert.Equal(HttpStatusCode.OK, freshInstance.StatusCode);
            Assert.Equal(HttpStatusCode.OK, freshInstances.StatusCode);
            var page = await freshInstances.Content
                .ReadFromJsonAsync<SystemInstancePageDocument>();
            Assert.Contains(page!.Items,
                item => item.SystemInstanceId == instance.SystemInstanceId);
            Assert.Contains(page.Items,
                item => item.SystemInstanceId == independentInstance.SystemInstanceId);
            Assert.Contains(page.Items,
                item => item.SystemInstanceId == recovery.SystemInstanceId);
        }
        using var firstRevisionResponse = await owner.GetAsync($"{applicationPath}/revisions/1");
        Assert.Equal(HttpStatusCode.OK, firstRevisionResponse.StatusCode);
        var firstRevision = await firstRevisionResponse.Content
            .ReadFromJsonAsync<ApplicationRevisionDocument>();
        Assert.Equal("Run payroll", firstRevision?.Purpose);
        Assert.Equal("declared", firstRevision?.ChangeKind);
        using var historyResponse = await owner.GetAsync(
            $"{applicationPath}/revisions?limit=1&minimum_application_revision=2");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = await historyResponse.Content
            .ReadFromJsonAsync<ApplicationRevisionPageDocument>();
        Assert.Equal([1L], history?.Items.Select(item => item.Revision));
        Assert.NotNull(history?.NextCursor);
        using var nextHistoryResponse = await owner.GetAsync(
            $"{applicationPath}/revisions?limit=1&cursor={Uri.EscapeDataString(history.NextCursor)}");
        Assert.Equal(HttpStatusCode.OK, nextHistoryResponse.StatusCode);
        var nextHistory = await nextHistoryResponse.Content
            .ReadFromJsonAsync<ApplicationRevisionPageDocument>();
        Assert.Equal([2L], nextHistory?.Items.Select(item => item.Revision));
        using var deniedInstance = await outsider.GetAsync(instancePath);
        using var deniedInstances = await outsider.GetAsync(
            $"{applicationPath}/system-instances?minimum_application_revision=2");
        using var deniedHistory = await outsider.GetAsync($"{applicationPath}/revisions/1");
        using var deniedHistoryList = await outsider.GetAsync($"{applicationPath}/revisions");
        Assert.Equal(HttpStatusCode.NotFound, deniedInstance.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedInstances.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedHistory.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedHistoryList.StatusCode);
        await using (var mcp = await McpScenario.ConnectAsync(owner,
                         new Uri(owner.BaseAddress!, "/mcp")))
        {
            _ = await mcp.When("bdgrz.application.revision.get", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenant.TenantId,
                ["application_id"] = registration.ApplicationId,
                ["revision"] = 1,
            }).ExpectSuccess();
            _ = await mcp.When("bdgrz.application.revision.list", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenant.TenantId,
                ["application_id"] = registration.ApplicationId,
                ["minimum_application_revision"] = 2,
            }).ExpectSuccess();
            var mcpInstance = await mcp.When("bdgrz.system_instance.get", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenant.TenantId,
                ["application_id"] = registration.ApplicationId,
                ["system_instance_id"] = instance.SystemInstanceId,
                ["minimum_application_revision"] = 2,
                ["minimum_instance_revision"] = 1,
            }).ExpectSuccess();
            Assert.Equal(1, Assert.IsType<JsonElement>(mcpInstance.StructuredJson)
                .GetProperty("result").GetProperty("revision").GetInt64());
            _ = await mcp.When("bdgrz.system_instance.list", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenant.TenantId,
                ["application_id"] = registration.ApplicationId,
                ["minimum_application_revision"] = 2,
            }).ExpectSuccess();
            _ = await mcp.When("bdgrz.system_instance.declare", new Dictionary<string, object?>
            {
                ["tenant_id"] = tenant.TenantId,
                ["application_id"] = registration.ApplicationId,
                ["expected_application_revision"] = 1,
                ["name"] = "MCP testing",
                ["kind"] = "testing",
            }).ExpectSuccess();
        }
        var programId = await CreateProgramAsync(owner, tenant.TenantId);
        var boundaryPath = $"/api/v1/tenants/{tenant.TenantId}/programs/{programId}/boundaries";
        using var applicationBoundary = await owner.PostAsJsonAsync(boundaryPath,
            new { content = BoundaryContent("application", registration.ApplicationId) });
        using var instanceBoundary = await owner.PostAsJsonAsync(boundaryPath,
            new { content = BoundaryContent("system_instance", instance.SystemInstanceId) });
        Assert.Equal(HttpStatusCode.OK, applicationBoundary.StatusCode);
        Assert.Equal(HttpStatusCode.OK, instanceBoundary.StatusCode);
        using var deniedBoundary = await outsider.PostAsJsonAsync(boundaryPath,
            new { content = BoundaryContent("system_instance", instance.SystemInstanceId) });
        Assert.Equal(HttpStatusCode.NotFound, deniedBoundary.StatusCode);
        using var otherTenantResponse = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Other application tenant",
            slug = $"other-app-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, otherTenantResponse.StatusCode);
        var otherTenant = await otherTenantResponse.Content.ReadFromJsonAsync<TenantDocument>();
        Assert.NotNull(otherTenant);
        var otherApplicationsPath = $"/api/v1/tenants/{otherTenant.TenantId}/applications";
        ApplicationRegistrationDocument? otherRegistration = null;
        lastStatus = null;
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(otherApplicationsPath, new
            {
                name = "Other system",
                purpose = "Establish second tenant access",
            });
            if (response.StatusCode == HttpStatusCode.OK)
            {
                otherRegistration = await response.Content
                    .ReadFromJsonAsync<ApplicationRegistrationDocument>();
                break;
            }
            lastStatus = response.StatusCode;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        Assert.True(otherRegistration is not null,
            $"Second-tenant declaration never became authorized; last status: {lastStatus}.");
        Assert.NotNull(otherRegistration);
        using var crossTenantApplication = await owner.GetAsync(
            $"{otherApplicationsPath}/{registration.ApplicationId}");
        using var crossTenantPreview = await owner.PostAsJsonAsync(
            $"{otherApplicationsPath}/{registration.ApplicationId}/change-previews", new
            {
                expected_application_revision = 3,
                change_kind = "retire",
            });
        using var crossTenantInstance = await owner.GetAsync(
            $"{otherApplicationsPath}/{registration.ApplicationId}/system-instances/{instance.SystemInstanceId}");
        using var crossTenantInstances = await owner.GetAsync(
            $"{otherApplicationsPath}/{registration.ApplicationId}/system-instances?minimum_application_revision=3");
        using var crossTenantHistory = await owner.GetAsync(
            $"{otherApplicationsPath}/{registration.ApplicationId}/revisions/1");
        using var crossTenantHistoryList = await owner.GetAsync(
            $"{otherApplicationsPath}/{registration.ApplicationId}/revisions");
        Assert.Equal(HttpStatusCode.NotFound, crossTenantApplication.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantPreview.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantInstance.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantInstances.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantHistory.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantHistoryList.StatusCode);
        var otherProgramId = await CreateProgramAsync(owner, otherTenant.TenantId);
        var otherBoundaryPath = $"/api/v1/tenants/{otherTenant.TenantId}/programs/" +
            $"{otherProgramId}/boundaries";
        using var foreignApplication = await owner.PostAsJsonAsync(otherBoundaryPath,
            new { content = BoundaryContent("application", registration.ApplicationId) });
        using var foreignInstance = await owner.PostAsJsonAsync(otherBoundaryPath,
            new { content = BoundaryContent("system_instance", instance.SystemInstanceId) });
        using var absentInstance = await owner.PostAsJsonAsync(otherBoundaryPath,
            new { content = BoundaryContent("system_instance", Guid.NewGuid()) });
        Assert.Equal(HttpStatusCode.Conflict, foreignApplication.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, foreignInstance.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, absentInstance.StatusCode);
        const string missingReference =
            "The governed system instance reference is unavailable in this tenant.";
        Assert.Contains(missingReference,
            await foreignInstance.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Contains(missingReference,
            await absentInstance.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    sealed record TenantDocument([property: JsonPropertyName("tenant_id")] Guid TenantId);
    sealed record ProgramRegistrationDocument(
        [property: JsonPropertyName("program_id")] Guid ProgramId);
    static async Task<Guid> RecordPersonAsync(HttpClient owner, string peoplePath,
        string displayName)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(peoplePath,
                new { display_name = displayName });
            if (response.StatusCode == HttpStatusCode.OK)
                return (await response.Content.ReadFromJsonAsync<PersonRegistrationDocument>())!
                    .PersonId;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new InvalidOperationException("Person recording never became authorized.");
    }

    sealed record PersonRegistrationDocument(
        [property: JsonPropertyName("person_id")] Guid PersonId);
    sealed record BoundaryRegistrationDocument(
        [property: JsonPropertyName("boundary_id")] Guid BoundaryId);
    sealed record ControlRegistrationDocument(
        [property: JsonPropertyName("control_id")] Guid ControlId);
    sealed record ApplicationRegistrationDocument(
        [property: JsonPropertyName("application_id")] Guid ApplicationId);
    sealed record ApplicationDocument(
        [property: JsonPropertyName("application_id")] Guid ApplicationId,
        [property: JsonPropertyName("revision")] long Revision,
        [property: JsonPropertyName("source_kind")] string SourceKind,
        [property: JsonPropertyName("classification")] string? Classification,
        [property: JsonPropertyName("system_owner_person_id")] Guid? SystemOwnerPersonId,
        [property: JsonPropertyName("access_owner_person_id")] Guid? AccessOwnerPersonId,
        [property: JsonPropertyName("unresolved")] string[] Unresolved);
    sealed record ApplicationPageDocument(
        [property: JsonPropertyName("items")] ApplicationDocument[] Items,
        [property: JsonPropertyName("next_cursor")] string? NextCursor);
    sealed record ApplicationRevisionDocument(
        [property: JsonPropertyName("revision")] long Revision,
        [property: JsonPropertyName("purpose")] string Purpose,
        [property: JsonPropertyName("classification")] string? Classification,
        [property: JsonPropertyName("change_kind")] string ChangeKind,
        [property: JsonPropertyName("system_instance_id")] Guid? SystemInstanceId,
        [property: JsonPropertyName("system_instance")] SystemInstanceDocument? SystemInstance);
    sealed record ApplicationRevisionPageDocument(
        [property: JsonPropertyName("items")] ApplicationRevisionDocument[] Items,
        [property: JsonPropertyName("next_cursor")] string? NextCursor);
    sealed record SystemInstanceRegistrationDocument(
        [property: JsonPropertyName("system_instance_id")] Guid SystemInstanceId);
    sealed record SystemInstanceDocument(
        [property: JsonPropertyName("system_instance_id")] Guid SystemInstanceId,
        [property: JsonPropertyName("source_kind")] string SourceKind,
        [property: JsonPropertyName("source_identifier")] string? SourceIdentifier,
        [property: JsonPropertyName("unresolved")] string[] Unresolved)
    {
        [JsonPropertyName("revision")]
        public long Revision { get; init; }

        [JsonPropertyName("legacy_application_revision")]
        public long? LegacyApplicationRevision { get; init; }
    }
    sealed record SystemInstancePageDocument(
        [property: JsonPropertyName("items")] SystemInstanceDocument[] Items);
}

// Keep inventory bootstrap timing independent of tenant history from other broker tests.
