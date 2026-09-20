using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Hosting;

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
        using var worker = BuildWorker(applicationName);
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
        await worker.StopAsync();
        using var unprojectedInstanceResponse = await owner.PostAsJsonAsync(
            $"{applicationsPath}/{first.ApplicationId}/system_instances", new
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
        var boundaryPath = $"/api/v1/tenants/{tenant.TenantId}/programs/{programId}/boundaries";
        var instanceContent = BoundaryContent("system_instance",
            unprojectedInstance.SystemInstanceId);
        using var laggingReference = await owner.PostAsJsonAsync(boundaryPath,
            new { content = instanceContent });
        Assert.Equal(HttpStatusCode.Conflict, laggingReference.StatusCode);
        using var restarted = BuildWorker(applicationName);
        await restarted.StartAsync();
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        try
        {
            var instanceProjected = false;
            while (DateTimeOffset.UtcNow < deadline)
            {
                using var response = await owner.GetAsync(
                    $"{applicationsPath}/{first.ApplicationId}/system_instances/" +
                    unprojectedInstance.SystemInstanceId);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    instanceProjected = true;
                    break;
                }
                await Task.Delay(250);
            }
            Assert.True(instanceProjected);
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
            while (DateTimeOffset.UtcNow < deadline)
            {
                using var response = await owner.GetAsync(
                    $"/api/v1/tenants/{tenant.TenantId}/boundaries/{linkedBoundary.BoundaryId}");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    boundaryProjected = true;
                    break;
                }
                await Task.Delay(250);
            }
            Assert.True(boundaryProjected);
            await using (var mcp = await McpScenario.ConnectAsync(owner,
                             new Uri(owner.BaseAddress!, "/mcp")))
            {
                _ = await mcp.When("bdgrz.boundary.get", new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenant.TenantId,
                    ["boundary_id"] = linkedBoundary.BoundaryId,
                }).ExpectSuccess();
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
                $"{applicationsPath}/{second.ApplicationId}/system_instances", new
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
                    $"{applicationsPath}/{second.ApplicationId}/system_instances/{instance.SystemInstanceId}");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    projectedInstance = await response.Content.ReadFromJsonAsync<SystemInstanceDocument>();
                    break;
                }
                await Task.Delay(250);
            }
            Assert.Equal("benefits-prod", projectedInstance?.SourceIdentifier);
            ApplicationRevisionDocument? splitRevision = null;
            deadline = DateTimeOffset.UtcNow.AddSeconds(45);
            while (DateTimeOffset.UtcNow < deadline)
            {
                using var response = await owner.GetAsync(
                    $"{applicationsPath}/{second.ApplicationId}/revisions/2");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    splitRevision = await response.Content
                        .ReadFromJsonAsync<ApplicationRevisionDocument>();
                    break;
                }
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                await Task.Delay(250);
            }
            Assert.Equal("system_instance_declared", splitRevision?.ChangeKind);
            Assert.Equal(instance.SystemInstanceId, splitRevision?.SystemInstanceId);
            using var wrongApplication = await owner.GetAsync(
                $"{applicationsPath}/{first.ApplicationId}/system_instances/{instance.SystemInstanceId}");
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

    IHost BuildWorker(string applicationName)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
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
        Assert.Contains("classification_unresolved", application.Unresolved);
        Assert.Contains("owner_unverified", application.Unresolved);
        using var future = await owner.GetAsync($"{applicationPath}?minimum_revision=2");
        Assert.Equal(HttpStatusCode.Conflict, future.StatusCode);
        using var denied = await outsider.GetAsync(applicationPath);
        using var deniedList = await outsider.GetAsync(applicationsPath);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedList.StatusCode);
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
            }).ExpectSuccess();
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
        using var instanceResponse = await owner.PostAsJsonAsync(
            $"{applicationPath}/system_instances", new
            {
                expected_application_revision = 1,
                name = "Production",
                kind = "production",
                source_identifier = "payroll-prod",
            });
        Assert.Equal(HttpStatusCode.OK, instanceResponse.StatusCode);
        var instance = await instanceResponse.Content
            .ReadFromJsonAsync<SystemInstanceRegistrationDocument>();
        Assert.NotNull(instance);
        using var stale = await owner.PostAsJsonAsync($"{applicationPath}/system_instances", new
        {
            expected_application_revision = 1,
            name = "Staging",
            kind = "staging",
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var instancePath = $"{applicationPath}/system_instances/{instance.SystemInstanceId}";
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
        Assert.Contains("source_identifier_unverified", projected.Unresolved);
        ApplicationRevisionDocument? instanceRevision = null;
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync($"{applicationPath}/revisions/2");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                instanceRevision = await response.Content
                    .ReadFromJsonAsync<ApplicationRevisionDocument>();
                break;
            }
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await Task.Delay(250);
        }
        Assert.Equal("system_instance_declared", instanceRevision?.ChangeKind);
        Assert.Equal(instance.SystemInstanceId, instanceRevision?.SystemInstanceId);
        Assert.Equal("payroll-prod", instanceRevision?.SystemInstance?.SourceIdentifier);
        using var revisedResponse = await owner.PutAsJsonAsync(applicationPath, new
        {
            expected_revision = 2,
            name = "Payroll",
            purpose = "Run monthly payroll",
            owner_reference = "Finance",
        });
        Assert.Equal(HttpStatusCode.NoContent, revisedResponse.StatusCode);
        ApplicationRevisionDocument? revisedRevision = null;
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync($"{applicationPath}/revisions/3");
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
        using var firstRevisionResponse = await owner.GetAsync($"{applicationPath}/revisions/1");
        Assert.Equal(HttpStatusCode.OK, firstRevisionResponse.StatusCode);
        var firstRevision = await firstRevisionResponse.Content
            .ReadFromJsonAsync<ApplicationRevisionDocument>();
        Assert.Equal("Run payroll", firstRevision?.Purpose);
        Assert.Equal("declared", firstRevision?.ChangeKind);
        using var historyResponse = await owner.GetAsync(
            $"{applicationPath}/revisions?limit=2&minimum_application_revision=3");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = await historyResponse.Content
            .ReadFromJsonAsync<ApplicationRevisionPageDocument>();
        Assert.Equal([1L, 2L], history?.Items.Select(item => item.Revision));
        Assert.NotNull(history?.NextCursor);
        using var nextHistoryResponse = await owner.GetAsync(
            $"{applicationPath}/revisions?limit=2&cursor={Uri.EscapeDataString(history.NextCursor)}");
        Assert.Equal(HttpStatusCode.OK, nextHistoryResponse.StatusCode);
        var nextHistory = await nextHistoryResponse.Content
            .ReadFromJsonAsync<ApplicationRevisionPageDocument>();
        Assert.Equal([3L], nextHistory?.Items.Select(item => item.Revision));
        using var deniedInstance = await outsider.GetAsync(instancePath);
        using var deniedHistory = await outsider.GetAsync($"{applicationPath}/revisions/1");
        using var deniedHistoryList = await outsider.GetAsync($"{applicationPath}/revisions");
        Assert.Equal(HttpStatusCode.NotFound, deniedInstance.StatusCode);
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
                ["minimum_application_revision"] = 3,
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
        using var crossTenantInstance = await owner.GetAsync(
            $"{otherApplicationsPath}/{registration.ApplicationId}/system_instances/{instance.SystemInstanceId}");
        using var crossTenantHistory = await owner.GetAsync(
            $"{otherApplicationsPath}/{registration.ApplicationId}/revisions/1");
        using var crossTenantHistoryList = await owner.GetAsync(
            $"{otherApplicationsPath}/{registration.ApplicationId}/revisions");
        Assert.Equal(HttpStatusCode.NotFound, crossTenantApplication.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantInstance.StatusCode);
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
    sealed record BoundaryRegistrationDocument(
        [property: JsonPropertyName("boundary_id")] Guid BoundaryId);
    sealed record ApplicationRegistrationDocument(
        [property: JsonPropertyName("application_id")] Guid ApplicationId);
    sealed record ApplicationDocument(
        [property: JsonPropertyName("application_id")] Guid ApplicationId,
        [property: JsonPropertyName("revision")] long Revision,
        [property: JsonPropertyName("source_kind")] string SourceKind,
        [property: JsonPropertyName("unresolved")] string[] Unresolved);
    sealed record ApplicationPageDocument(
        [property: JsonPropertyName("items")] ApplicationDocument[] Items,
        [property: JsonPropertyName("next_cursor")] string? NextCursor);
    sealed record ApplicationRevisionDocument(
        [property: JsonPropertyName("revision")] long Revision,
        [property: JsonPropertyName("purpose")] string Purpose,
        [property: JsonPropertyName("change_kind")] string ChangeKind,
        [property: JsonPropertyName("system_instance_id")] Guid? SystemInstanceId,
        [property: JsonPropertyName("system_instance")] SystemInstanceDocument? SystemInstance);
    sealed record ApplicationRevisionPageDocument(
        [property: JsonPropertyName("items")] ApplicationRevisionDocument[] Items,
        [property: JsonPropertyName("next_cursor")] string? NextCursor);
    sealed record SystemInstanceRegistrationDocument(
        [property: JsonPropertyName("system_instance_id")] Guid SystemInstanceId);
    sealed record SystemInstanceDocument(
        [property: JsonPropertyName("source_kind")] string SourceKind,
        [property: JsonPropertyName("source_identifier")] string? SourceIdentifier,
        [property: JsonPropertyName("unresolved")] string[] Unresolved);
}

// Keep inventory bootstrap timing independent of tenant history from other broker tests.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ApplicationInventoryBrokerCollectionDefinition
    : ICollectionFixture<BrokerStackFixture>
{
    public const string Name = "Application inventory broker e2e";
}
