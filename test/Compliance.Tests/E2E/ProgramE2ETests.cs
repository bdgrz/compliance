using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ProgramE2ETests(BrokerStackFixture broker) : IClassFixture<BrokerStackFixture>
{
    static readonly string[] SecurityCategory = ["security"];

    [Fact]
    public async Task ShouldProjectProgramChangesGivenIndependentWorker()
    {
        // Arrange
        var applicationName = $"compliance-program-split-{Guid.NewGuid():N}";
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
        using var worker = builder.Build();

        // Act
        await worker.StartAsync();

        // Assert
        try
        {
            await using var factory = E2EAppFactory.Create(broker, applicationName);
            var priorMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
            HttpClient owner;
            try
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", "api");
                owner = factory.CreateClient();
            }
            finally
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", priorMode);
            }
            using (owner)
            {
                var ownerEmail = $"program-split-owner-{Guid.NewGuid():N}@example.com";
                var ownerUserId = await TenantInvitationE2ETests.LoginAsync(owner, ownerEmail);
                using var tenantResponse = await owner.PostAsJsonAsync("/api/v1/tenants", new
                {
                    name = "Split Program",
                    slug = $"split-program-{Guid.NewGuid():N}"[..24],
                });
                Assert.Equal(HttpStatusCode.OK, tenantResponse.StatusCode);
                var tenant = await tenantResponse.Content.ReadFromJsonAsync<TenantRegistrationDocument>();
                Assert.NotNull(tenant);
                var path = $"/api/v1/tenants/{tenant.TenantId}/programs";
                var plan = new
                {
                    target_readiness_date = "2027-01-31",
                    target_type_i_as_of_date = "2027-03-31",
                    target_type_ii_start_date = "2027-04-01",
                    target_type_ii_end_date = "2028-03-31",
                    readiness_advisor = "Advisor A",
                    audit_firm = (string?)null,
                };
                ProgramRegistrationDocument? registration = null;
                var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
                while (DateTimeOffset.UtcNow < deadline)
                {
                    using var response = await owner.PostAsJsonAsync(path,
                        new { name = "Split program", plan });
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        registration = await response.Content.ReadFromJsonAsync<ProgramRegistrationDocument>();
                        break;
                    }
                    Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                        await response.Content.ReadAsStringAsync());
                    await Task.Delay(250);
                }
                Assert.NotNull(registration);
                var programPath = $"{path}/{registration.ProgramId}";
                ProgramDocument? projected = null;
                while (DateTimeOffset.UtcNow < deadline)
                {
                    using var response = await owner.GetAsync(programPath);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        projected = await response.Content.ReadFromJsonAsync<ProgramDocument>();
                        if (projected?.Revision == 1)
                            break;
                    }
                    await Task.Delay(250);
                }
                Assert.Equal("Split program", projected?.Name);
                var tenantId = Uuid.Parse(tenant.TenantId, CultureInfo.InvariantCulture);
                var programId = Uuid.Parse(registration.ProgramId, CultureInfo.InvariantCulture);
                ProgramCreated? storedProgram = null;
                await foreach (var record in worker.Services.GetRequiredService<IEventStore>()
                                   .ReadAsync(new ComplianceProgram(tenantId, programId).Stream, 0,
                                       CancellationToken.None))
                {
                    storedProgram = Assert.IsType<ProgramCreated>(record.Event);
                }
                Assert.Equal(ActorReference.ForMember(RbacIds.Member(tenantId,
                        Uuid.Parse(ownerUserId, CultureInfo.InvariantCulture)),
                    ownerEmail), storedProgram?.StoredActor);
                var administratorsTeamId = BuiltInRbac.AdministratorsTeamId(tenantId);
                ActorReference? bootstrapActor = null;
                await foreach (var record in worker.Services.GetRequiredService<IEventStore>()
                                   .ReadAsync(new Team(tenantId, administratorsTeamId).Stream, 0,
                                       CancellationToken.None))
                {
                    if (record.Event is TeamDefined)
                        bootstrapActor = ActorReference.FromSystemMetadata(record.Event.Metadata);
                }
                Assert.Equal(ActorReference.ForSystemProcess("reactor:TenantRbacBootstrap",
                    "TenantRbacBootstrap"), bootstrapActor);
                using var actorResponse = await owner.GetAsync(programPath);
                Assert.Equal(HttpStatusCode.OK, actorResponse.StatusCode);
                using var actorDocument = JsonDocument.Parse(
                    await actorResponse.Content.ReadAsStringAsync());
                var actorView = actorDocument.RootElement.GetProperty("last_changed_by");
                Assert.Equal("member", actorView.GetProperty("kind").GetString());
                Assert.Equal(ownerEmail, actorView.GetProperty("display").GetString());
                await using (var actorMcp = await McpScenario.ConnectAsync(owner,
                                 new Uri(owner.BaseAddress!, "/mcp")))
                {
                    var toolResult = await actorMcp.When("bdgrz.program.get",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = tenant.TenantId,
                            ["program_id"] = registration.ProgramId,
                        }).ExpectSuccess();
                    var toolView = Assert.IsType<JsonElement>(toolResult.StructuredJson)
                        .GetProperty("result").GetProperty("last_changed_by");
                    Assert.Equal("member", toolView.GetProperty("kind").GetString());
                    Assert.Equal(ownerEmail, toolView.GetProperty("display").GetString());
                }
                using var pendingSplitRevision = await owner.GetAsync(
                    $"{programPath}?minimum_revision=2");
                Assert.Equal(HttpStatusCode.Conflict, pendingSplitRevision.StatusCode);
                using var initialSetupResponse = await owner.GetAsync($"{programPath}/setup-work");
                Assert.Equal(HttpStatusCode.OK, initialSetupResponse.StatusCode);
                var initialSetup = await initialSetupResponse.Content
                    .ReadFromJsonAsync<ProgramSetupDocument>();
                Assert.Contains(initialSetup?.Items ?? [], item => item.Code == "define_system_boundary");
                using var revised = await owner.PutAsJsonAsync(programPath,
                    new { expected_revision = 1, name = "Split program revised", plan });
                Assert.Equal(HttpStatusCode.NoContent, revised.StatusCode);
                while (DateTimeOffset.UtcNow < deadline)
                {
                    using var response = await owner.GetAsync(programPath);
                    projected = await response.Content.ReadFromJsonAsync<ProgramDocument>();
                    if (projected?.Revision == 2)
                        break;
                    await Task.Delay(250);
                }
                Assert.Equal("Split program revised", projected?.Name);
                Assert.Equal(2, projected?.Revision);
                using var splitExactRevision = await owner.GetAsync(
                    $"{programPath}/revisions/2");
                using var splitHistoryAnchor = await owner.GetAsync(
                    $"{programPath}/revisions?minimum_program_revision=2");
                Assert.Equal(HttpStatusCode.OK, splitExactRevision.StatusCode);
                Assert.Equal(HttpStatusCode.OK, splitHistoryAnchor.StatusCode);
                using var revisionDocument = JsonDocument.Parse(
                    await splitExactRevision.Content.ReadAsStringAsync());
                Assert.Equal("member", revisionDocument.RootElement.GetProperty("actor")
                    .GetProperty("kind").GetString());
                using var currentSplitRevision = await owner.GetAsync(
                    $"{programPath}?minimum_revision=2");
                Assert.Equal(HttpStatusCode.OK, currentSplitRevision.StatusCode);
                using var currentSetupRevision = await owner.GetAsync(
                    $"{programPath}/setup-work?minimum_program_revision=2");
                using var futureSetupRevision = await owner.GetAsync(
                    $"{programPath}/setup-work?minimum_program_revision=3");
                Assert.Equal(HttpStatusCode.OK, currentSetupRevision.StatusCode);
                Assert.Equal(HttpStatusCode.Conflict, futureSetupRevision.StatusCode);

                var servicesPath = $"/api/v1/tenants/{tenant.TenantId}/client-services";
                using var serviceCreated = await owner.PostAsJsonAsync(
                    $"{programPath}/client-services", new
                    {
                        name = "Service A",
                        purpose = "Handle customer requests",
                        owner_reference = "Operations",
                    });
                Assert.Equal(HttpStatusCode.OK, serviceCreated.StatusCode);
                var service = await serviceCreated.Content
                    .ReadFromJsonAsync<ClientServiceRegistrationDocument>();
                Assert.NotNull(service);
                ClientServiceDocument? serviceView = null;
                while (DateTimeOffset.UtcNow < deadline)
                {
                    using var response = await owner.GetAsync($"{servicesPath}/{service.ServiceId}");
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        serviceView = await response.Content.ReadFromJsonAsync<ClientServiceDocument>();
                        if (serviceView?.Status == "active")
                            break;
                    }
                    await Task.Delay(250);
                }
                Assert.Equal("active", serviceView?.Status);
                using var splitServiceRevision = await owner.GetAsync(
                    $"/api/v1/tenants/{tenant.TenantId}/client-services/{service.ServiceId}/revisions/1");
                Assert.Equal(HttpStatusCode.OK, splitServiceRevision.StatusCode);

                var boundariesPath = $"{programPath}/boundaries";
                using var boundaryCreated = await owner.PostAsJsonAsync(boundariesPath, new
                {
                    content = new
                    {
                        statement = "Split-host service boundary",
                        engagement_stage = "readiness",
                        trust_services_categories = new List<string> { "security" },
                        entries = new object[]
                        {
                            new
                            {
                                entry_id = Uuid.CreateVersion4().ToString(),
                                kind = "inclusion",
                                subject_type = "service",
                                subject = "Service A",
                                governed_record_id = service.ServiceId,
                                owner_reference = "Compliance lead",
                                rationale = "It handles customer requests.",
                                unresolved = false,
                            },
                            new
                            {
                                entry_id = Uuid.CreateVersion4().ToString(),
                                kind = "assumption",
                                subject_type = "provider",
                                subject = "Pending hosting provider",
                                governed_record_id = (string?)null,
                                owner_reference = "Compliance lead",
                                rationale = "Confirm provider scope before assessment.",
                                unresolved = true,
                            },
                        },
                    },
                });
                Assert.Equal(HttpStatusCode.OK, boundaryCreated.StatusCode);
                var boundary = await boundaryCreated.Content
                    .ReadFromJsonAsync<BoundaryRegistrationDocument>();
                Assert.NotNull(boundary);
                var boundaryPath = $"/api/v1/tenants/{tenant.TenantId}/boundaries/{boundary.BoundaryId}";
                BoundaryDocument? boundaryView = null;
                while (DateTimeOffset.UtcNow < deadline)
                {
                    using var response = await owner.GetAsync(boundaryPath);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        boundaryView = await response.Content.ReadFromJsonAsync<BoundaryDocument>();
                        if (boundaryView?.Draft?.Revision == 1)
                            break;
                    }
                    await Task.Delay(250);
                }
                Assert.Equal(boundary.DraftVersionId, boundaryView?.Draft?.VersionId);
                Assert.Equal(1, boundaryView?.Revision);
                using var currentBoundaryRevision = await owner.GetAsync(
                    $"{boundaryPath}?minimum_revision=1");
                using var futureBoundaryRevision = await owner.GetAsync(
                    $"{boundaryPath}?minimum_revision=2");
                Assert.Equal(HttpStatusCode.OK, currentBoundaryRevision.StatusCode);
                Assert.Equal(HttpStatusCode.Conflict, futureBoundaryRevision.StatusCode);
                var setupAnchor = $"{programPath}/setup-work?boundary_id={boundary.BoundaryId}";
                using var currentBoundarySetup = await owner.GetAsync(
                    $"{setupAnchor}&minimum_boundary_revision=1");
                using var futureBoundarySetup = await owner.GetAsync(
                    $"{setupAnchor}&minimum_boundary_revision=2");
                Assert.Equal(HttpStatusCode.OK, currentBoundarySetup.StatusCode);
                Assert.Equal(HttpStatusCode.Conflict, futureBoundarySetup.StatusCode);
                var versionPath = $"{boundaryPath}/versions/{boundary.DraftVersionId}";
                var versionsPath = $"{boundaryPath}/versions";
                var effectivePath = $"{boundaryPath}/effective-version?effective_on=2027-01-01";
                using var splitVersion = await owner.GetAsync(
                    $"{versionPath}?minimum_boundary_revision=1");
                using var splitVersions = await owner.GetAsync(
                    $"{versionsPath}?minimum_boundary_revision=1");
                using var splitEffective = await owner.GetAsync(
                    $"{effectivePath}&minimum_boundary_revision=1");
                using var splitFutureVersion = await owner.GetAsync(
                    $"{versionPath}?minimum_boundary_revision=2");
                using var splitFutureVersions = await owner.GetAsync(
                    $"{versionsPath}?minimum_boundary_revision=2");
                using var splitFutureEffective = await owner.GetAsync(
                    $"{effectivePath}&minimum_boundary_revision=2");
                Assert.Equal(HttpStatusCode.NotFound, splitVersion.StatusCode);
                Assert.Equal(HttpStatusCode.OK, splitVersions.StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, splitEffective.StatusCode);
                Assert.Equal(HttpStatusCode.Conflict, splitFutureVersion.StatusCode);
                Assert.Equal(HttpStatusCode.Conflict, splitFutureVersions.StatusCode);
                Assert.Equal(HttpStatusCode.Conflict, splitFutureEffective.StatusCode);
                ProgramSetupDocument? draftSetup = null;
                while (DateTimeOffset.UtcNow < deadline)
                {
                    using var response = await owner.GetAsync($"{programPath}/setup-work");
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        draftSetup = await response.Content.ReadFromJsonAsync<ProgramSetupDocument>();
                        if (draftSetup?.Items.Any(item => item.Code == "approve_system_boundary") == true)
                            break;
                    }
                    await Task.Delay(250);
                }
                Assert.Contains(draftSetup?.Items ?? [], item =>
                    item.Code == "approve_system_boundary" && item.SourceId == boundary.BoundaryId);
                Assert.Contains(draftSetup?.Items ?? [], item =>
                    item.Code == "resolve_scope_references" && item.SourceId == boundary.BoundaryId);
                Assert.DoesNotContain(draftSetup?.Items ?? [], item => item.Code == "define_system_boundary");
                using var secondBoundaryCreated = await owner.PostAsJsonAsync(boundariesPath, new
                {
                    content = new
                    {
                        statement = "Second split-host boundary",
                        engagement_stage = "readiness",
                        trust_services_categories = SecurityCategory,
                        entries = Array.Empty<object>(),
                    },
                });
                Assert.Equal(HttpStatusCode.OK, secondBoundaryCreated.StatusCode);
                ProgramSetupDocument? firstSetupPage = null;
                while (DateTimeOffset.UtcNow < deadline)
                {
                    using var response = await owner.GetAsync($"{programPath}/setup-work?boundary_limit=1");
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        firstSetupPage = await response.Content.ReadFromJsonAsync<ProgramSetupDocument>();
                        if (firstSetupPage?.NextBoundaryCursor is not null)
                            break;
                    }
                    await Task.Delay(250);
                }
                Assert.NotNull(firstSetupPage);
                Assert.NotNull(firstSetupPage.NextBoundaryCursor);
                using var nextSetupPageResponse = await owner.GetAsync(
                    $"{programPath}/setup-work?boundary_limit=1&boundary_cursor=" +
                    Uri.EscapeDataString(firstSetupPage.NextBoundaryCursor));
                Assert.Equal(HttpStatusCode.OK, nextSetupPageResponse.StatusCode);
                var nextSetupPage = await nextSetupPageResponse.Content
                    .ReadFromJsonAsync<ProgramSetupDocument>();
                Assert.Contains(nextSetupPage?.Items ?? [], item => item.Code == "approve_system_boundary");
                Assert.DoesNotContain(nextSetupPage?.Items ?? [], item => item.Code == "define_system_boundary");
                ProgramSetupDocument? allSetup = null;
                using (var response = await owner.GetAsync($"{programPath}/setup-work"))
                {
                    allSetup = await response.Content.ReadFromJsonAsync<ProgramSetupDocument>();
                }
                Assert.Contains(allSetup?.Items ?? [], item => item.Code == "identify_scoped_services");

                using var otherProgramCreated = await owner.PostAsJsonAsync(path,
                    new { name = "Other program", plan });
                Assert.Equal(HttpStatusCode.OK, otherProgramCreated.StatusCode);
                var otherProgram = await otherProgramCreated.Content
                    .ReadFromJsonAsync<ProgramRegistrationDocument>();
                Assert.NotNull(otherProgram);
                var unrelatedBoundaryPath =
                    $"{path}/{otherProgram.ProgramId}/setup-work?boundary_id={boundary.BoundaryId}" +
                    "&minimum_boundary_revision=1";
                var unrelatedBoundaryDeadline = DateTimeOffset.UtcNow.AddSeconds(45);
                HttpStatusCode unrelatedBoundaryStatus = default;
                while (DateTimeOffset.UtcNow < unrelatedBoundaryDeadline)
                {
                    using var unrelatedBoundarySetup = await owner.GetAsync(unrelatedBoundaryPath);
                    if (unrelatedBoundarySetup.StatusCode == HttpStatusCode.NotFound)
                    {
                        unrelatedBoundaryStatus = unrelatedBoundarySetup.StatusCode;
                        break;
                    }

                    Assert.Equal(HttpStatusCode.Conflict, unrelatedBoundarySetup.StatusCode);
                    Assert.Equal("true", unrelatedBoundarySetup.Headers
                        .GetValues("Portia-Transient").Single());
                    await Task.Delay(250);
                }
                Assert.Equal(HttpStatusCode.NotFound, unrelatedBoundaryStatus);
                using var crossProgramBoundary = await owner.PostAsJsonAsync(
                    $"{path}/{otherProgram.ProgramId}/boundaries", new
                    {
                        content = new
                        {
                            statement = "Wrong program service",
                            engagement_stage = "readiness",
                            trust_services_categories = SecurityCategory,
                            entries = new[]
                            {
                                new
                                {
                                    entry_id = Uuid.CreateVersion4().ToString(),
                                    kind = "inclusion",
                                    subject_type = "service",
                                    subject = "Service A",
                                    governed_record_id = service.ServiceId,
                                    owner_reference = "Compliance lead",
                                    rationale = "The service is owned by another program.",
                                    unresolved = false,
                                },
                            },
                        },
                    });
                Assert.Equal(HttpStatusCode.Conflict, crossProgramBoundary.StatusCode);
            }
        }
        finally
        {
            await worker.StopAsync();
        }
    }

    [Fact]
    public async Task ShouldReturnTransientSetupWorkGivenStoppedSplitWorker()
    {
        // Arrange
        var applicationName = $"compliance-setup-work-split-{Guid.NewGuid():N}";
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
        using var worker = builder.Build();
        await worker.StartAsync();
        IHost? restartedWorker = null;

        try
        {
            await using var factory = E2EAppFactory.Create(broker, applicationName);
            var priorMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
            HttpClient owner;
            HttpClient outsider;
            try
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", "api");
                owner = factory.CreateClient();
                outsider = factory.CreateClient();
            }
            finally
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", priorMode);
            }
            using (owner)
            using (outsider)
            {
                await TenantInvitationE2ETests.LoginAsync(owner,
                    $"setup-work-owner-{Guid.NewGuid():N}@example.com");
                await TenantInvitationE2ETests.LoginAsync(outsider,
                    $"setup-work-outsider-{Guid.NewGuid():N}@example.com");
                using var tenantResponse = await owner.PostAsJsonAsync("/api/v1/tenants", new
                {
                    name = "Setup work split tenant",
                    slug = $"setup-work-{Guid.NewGuid():N}"[..24],
                });
                Assert.Equal(HttpStatusCode.OK, tenantResponse.StatusCode);
                var tenant = await tenantResponse.Content.ReadFromJsonAsync<TenantRegistrationDocument>();
                Assert.NotNull(tenant);
                var programsPath = $"/api/v1/tenants/{tenant.TenantId}/programs";
                var plan = new
                {
                    target_readiness_date = "2027-01-31",
                    target_type_i_as_of_date = "2027-03-31",
                    target_type_ii_start_date = "2027-04-01",
                    target_type_ii_end_date = "2028-03-31",
                    readiness_advisor = "Setup work advisor",
                    audit_firm = (string?)null,
                };
                ProgramRegistrationDocument? registration = null;
                var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
                while (DateTimeOffset.UtcNow < deadline)
                {
                    using var response = await owner.PostAsJsonAsync(programsPath,
                        new { name = "Setup work program", plan });
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        registration = await response.Content.ReadFromJsonAsync<ProgramRegistrationDocument>();
                        break;
                    }
                    Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                        await response.Content.ReadAsStringAsync());
                    await Task.Delay(250);
                }
                Assert.NotNull(registration);
                var programPath = $"{programsPath}/{registration.ProgramId}";
                var setupPath = $"{programPath}/setup-work";
                ProgramSetupDocument? initialSetup = null;
                while (DateTimeOffset.UtcNow < deadline)
                {
                    using var response = await owner.GetAsync(setupPath);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        initialSetup = await response.Content.ReadFromJsonAsync<ProgramSetupDocument>();
                        if (initialSetup?.Items.Any(item => item.Code == "define_system_boundary") == true)
                            break;
                    }
                    await Task.Delay(250);
                }
                Assert.Contains(initialSetup?.Items ?? [], item => item.Code == "define_system_boundary");

                // Act
                await worker.StopAsync();
                using var boundaryResponse = await owner.PostAsJsonAsync($"{programPath}/boundaries",
                    new
                    {
                        content = new
                        {
                            statement = "Pending setup work boundary",
                            engagement_stage = "readiness",
                            trust_services_categories = SecurityCategory,
                            entries = Array.Empty<object>(),
                        },
                    });
                Assert.Equal(HttpStatusCode.OK, boundaryResponse.StatusCode);

                // Assert
                using (var pending = await owner.GetAsync(setupPath))
                {
                    Assert.Equal(HttpStatusCode.Conflict, pending.StatusCode);
                    Assert.Equal("true", pending.Headers.GetValues("Portia-Transient").Single());
                }
                var setupToolInput = new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenant.TenantId,
                    ["program_id"] = registration.ProgramId,
                };
                await using (var ownerMcp = await McpScenario.ConnectAsync(owner,
                                 new Uri(owner.BaseAddress!, "/mcp")))
                {
                    var pending = await ownerMcp.When("bdgrz.program.setup-work.get", setupToolInput)
                        .ExpectFailure("Conflict");
                    var structured = Assert.IsType<JsonElement>(pending.StructuredJson);
                    Assert.True(structured.GetProperty("isTransient").GetBoolean());
                }
                using (var undisclosed = await outsider.GetAsync(setupPath))
                {
                    Assert.Equal(HttpStatusCode.NotFound, undisclosed.StatusCode);
                    Assert.Equal("false", undisclosed.Headers.GetValues("Portia-Transient").Single());
                }
                await using (var outsiderMcp = await McpScenario.ConnectAsync(outsider,
                                 new Uri(outsider.BaseAddress!, "/mcp")))
                {
                    var undisclosed = await outsiderMcp.When("bdgrz.program.setup-work.get", setupToolInput)
                        .ExpectFailure();
                    if (undisclosed.StructuredJson is JsonElement structured &&
                        structured.TryGetProperty("isTransient", out var transient))
                        Assert.False(transient.GetBoolean());
                }

                restartedWorker = BuildWorker(applicationName);
                await restartedWorker.StartAsync();
                deadline = DateTimeOffset.UtcNow.AddSeconds(45);
                ProgramSetupDocument? recovered = null;
                while (DateTimeOffset.UtcNow < deadline)
                {
                    using var response = await owner.GetAsync(setupPath);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        recovered = await response.Content.ReadFromJsonAsync<ProgramSetupDocument>();
                        if (recovered?.Items.Any(item => item.Code == "approve_system_boundary") == true)
                            break;
                    }
                    else
                        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                    await Task.Delay(250);
                }
                Assert.Contains(recovered?.Items ?? [], item => item.Code == "approve_system_boundary");
                await using var recoveredMcp = await McpScenario.ConnectAsync(owner,
                    new Uri(owner.BaseAddress!, "/mcp"));
                _ = await recoveredMcp.When("bdgrz.program.setup-work.get", setupToolInput)
                    .ExpectSuccess();
            }
        }
        finally
        {
            if (restartedWorker is not null)
            {
                await restartedWorker.StopAsync();
                restartedWorker.Dispose();
            }
        }
    }

    [Fact]
    public async Task ShouldKeepProgramChangesScopedGivenTwoTenants()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var owner = factory.CreateClient();
        using var outsider = factory.CreateClient();
        await TenantInvitationE2ETests.LoginAsync(owner, $"program-owner-{Guid.NewGuid():N}@example.com");
        await TenantInvitationE2ETests.LoginAsync(outsider, $"program-outsider-{Guid.NewGuid():N}@example.com");

        // Act
        using var tenantResponse = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Program E2E",
            slug = $"program-{Guid.NewGuid():N}"[..24],
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, tenantResponse.StatusCode);
        var tenant = await tenantResponse.Content.ReadFromJsonAsync<TenantRegistrationDocument>();
        Assert.NotNull(tenant);
        var tenantId = Uuid.Parse(tenant.TenantId, CultureInfo.InvariantCulture);
        var path = $"/api/v1/tenants/{tenantId}/programs";
        var create = new
        {
            name = "SOC 2 program",
            plan = new
            {
                target_readiness_date = "2027-01-31",
                target_type_i_as_of_date = "2027-03-31",
                target_type_ii_start_date = "2027-04-01",
                target_type_ii_end_date = "2028-03-31",
                readiness_advisor = "Advisor A",
                audit_firm = (string?)null,
            },
        };
        ProgramRegistrationDocument? registration = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(path, create);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                registration = await response.Content.ReadFromJsonAsync<ProgramRegistrationDocument>();
                break;
            }
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        Assert.NotNull(registration);
        var programPath = $"{path}/{registration.ProgramId}";

        ProgramDocument? projected = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(programPath);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                projected = await response.Content.ReadFromJsonAsync<ProgramDocument>();
                if (projected?.Revision == 1)
                    break;
            }
            await Task.Delay(250);
        }
        Assert.Equal("SOC 2 program", projected?.Name);
        Assert.Equal("readiness", projected?.Stage);
        Assert.Equal("type_i", projected?.NextStage);
        Assert.Equal(["readiness", "type_i", "type_ii"],
            projected?.StagePlan.Select(stage => stage.Stage));
        Assert.Equal("Advisor A", projected?.Plan.ReadinessAdvisor);
        using var futureProgramRevision = await owner.GetAsync($"{programPath}?minimum_revision=2");
        using var invalidProgramRevision = await owner.GetAsync($"{programPath}?minimum_revision=0");
        using var missingProgramRevision = await owner.GetAsync(
            $"{path}/{Uuid.CreateVersion4()}?minimum_revision=1");
        Assert.Equal(HttpStatusCode.Conflict, futureProgramRevision.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidProgramRevision.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingProgramRevision.StatusCode);
        using var setupResponse = await owner.GetAsync($"{programPath}/setup-work");
        Assert.Equal(HttpStatusCode.OK, setupResponse.StatusCode);
        var setup = await setupResponse.Content.ReadFromJsonAsync<ProgramSetupDocument>();
        Assert.Contains(setup?.Items ?? [], item => item.Code == "define_system_boundary");
        using var invalidSetupRevision = await owner.GetAsync(
            $"{programPath}/setup-work?minimum_program_revision=0");
        using var unpairedBoundaryRevision = await owner.GetAsync(
            $"{programPath}/setup-work?minimum_boundary_revision=1");
        using var missingBoundaryRevision = await owner.GetAsync(
            $"{programPath}/setup-work?boundary_id={Uuid.CreateVersion4()}" +
            "&minimum_boundary_revision=1");
        Assert.Equal(HttpStatusCode.BadRequest, invalidSetupRevision.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unpairedBoundaryRevision.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingBoundaryRevision.StatusCode);
        using var deniedSetup = await outsider.GetAsync($"{programPath}/setup-work");
        using var deniedFreshSetup = await outsider.GetAsync(
            $"{programPath}/setup-work?minimum_program_revision=1");
        using var missingSetup = await outsider.GetAsync($"{path}/{Uuid.CreateVersion4()}/setup-work");
        Assert.Equal(HttpStatusCode.NotFound, deniedSetup.StatusCode);
        Assert.Equal(deniedSetup.StatusCode, missingSetup.StatusCode);
        Assert.Equal(deniedSetup.StatusCode, deniedFreshSetup.StatusCode);
        var setupToolInput = new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId,
            ["program_id"] = registration.ProgramId,
        };
        await using (var ownerMcp = await McpScenario.ConnectAsync(owner,
                         new Uri(owner.BaseAddress!, "/mcp")))
        {
            _ = await ownerMcp.When("bdgrz.program.setup-work.get", setupToolInput).ExpectSuccess();
        }
        await using (var outsiderMcp = await McpScenario.ConnectAsync(outsider,
                         new Uri(outsider.BaseAddress!, "/mcp")))
        {
            _ = await outsiderMcp.When("bdgrz.program.setup-work.get", setupToolInput).ExpectFailure();
        }

        using var denied = await outsider.GetAsync(programPath);
        using var deniedFresh = await outsider.GetAsync($"{programPath}?minimum_revision=1");
        using var missing = await outsider.GetAsync($"{path}/{Uuid.CreateVersion4()}");
        using var deniedList = await outsider.GetAsync(path);
        using var deniedCreate = await outsider.PostAsJsonAsync(path, create);
        using var deniedRevise = await outsider.PutAsJsonAsync(programPath, new
        {
            expected_revision = 1,
            name = "Unauthorized change",
            plan = create.plan,
        });
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(denied.StatusCode, deniedFresh.StatusCode);
        Assert.Equal(denied.StatusCode, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedList.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedCreate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedRevise.StatusCode);

        using var revised = await owner.PutAsJsonAsync(programPath, new
        {
            expected_revision = 1,
            name = "SOC 2 continuing program",
            plan = new
            {
                target_readiness_date = "2027-02-15",
                target_type_i_as_of_date = "2027-03-31",
                target_type_ii_start_date = "2027-04-01",
                target_type_ii_end_date = "2028-03-31",
                readiness_advisor = "Advisor B",
                audit_firm = (string?)null,
            },
        });
        Assert.Equal(HttpStatusCode.NoContent, revised.StatusCode);
        using var stale = await owner.PutAsJsonAsync(programPath, new
        {
            expected_revision = 1,
            name = "Stale program",
            plan = create.plan,
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var staleRevisionDetail = "The program changed. Current revision: 2. Reload it and retry.";
        Assert.Contains(staleRevisionDetail, await stale.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
        using var invalidCreate = await owner.PostAsJsonAsync(path,
            new { name = "", plan = create.plan });
        Assert.Equal(HttpStatusCode.BadRequest, invalidCreate.StatusCode);
        Assert.Contains("A program requires a name.",
            await invalidCreate.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        using var invalidCurrent = await owner.PutAsJsonAsync(programPath, new
        {
            expected_revision = 2,
            name = "",
            plan = create.plan,
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidCurrent.StatusCode);
        Assert.Contains("A program requires a name.",
            await invalidCurrent.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await using (var commandMcp = await McpScenario.ConnectAsync(owner,
                         new Uri(owner.BaseAddress!, "/mcp")))
        {
            var invalidCreateMcp = await commandMcp.When("bdgrz.program.create",
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenant.TenantId,
                    ["name"] = "",
                    ["plan"] = create.plan,
                }).ExpectFailure("Validation");
            Assert.Equal("A program requires a name.", Assert.IsType<JsonElement>(
                invalidCreateMcp.StructuredJson).GetProperty("message").GetString());
            var staleMcp = await commandMcp.When("bdgrz.program.revise",
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenant.TenantId,
                    ["program_id"] = registration.ProgramId,
                    ["expected_revision"] = 1,
                    ["name"] = "",
                    ["plan"] = create.plan,
                }).ExpectFailure("Conflict");
            Assert.Equal(staleRevisionDetail, Assert.IsType<JsonElement>(
                staleMcp.StructuredJson).GetProperty("message").GetString());
        }

        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(programPath);
            projected = await response.Content.ReadFromJsonAsync<ProgramDocument>();
            if (projected?.Revision == 2)
                break;
            await Task.Delay(250);
        }
        Assert.Equal(2, projected?.Revision);
        using var currentProgramRevision = await owner.GetAsync($"{programPath}?minimum_revision=2");
        Assert.Equal(HttpStatusCode.OK, currentProgramRevision.StatusCode);
        Assert.Equal("SOC 2 continuing program", projected?.Name);
        Assert.Equal("Advisor B", projected?.Plan.ReadinessAdvisor);
        using var revisionsResponse = await owner.GetAsync($"{programPath}/revisions");
        Assert.Equal(HttpStatusCode.OK, revisionsResponse.StatusCode);
        var revisions = await revisionsResponse.Content.ReadFromJsonAsync<ProgramRevisionPageDocument>();
        Assert.Equal([1L, 2L], revisions?.Items.Select(item => item.Revision));
        Assert.Equal("Advisor A", revisions?.Items[0].Plan.ReadinessAdvisor);
        Assert.Equal("Advisor B", revisions?.Items[1].Plan.ReadinessAdvisor);
        Assert.All(revisions?.Items ?? [], item => Assert.False(string.IsNullOrWhiteSpace(item.ActorDisplay)));
        var exactRevisionPath = $"{programPath}/revisions/1";
        using var exactRevision = await owner.GetAsync(exactRevisionPath);
        Assert.Equal(HttpStatusCode.OK, exactRevision.StatusCode);
        var immutableRevision = await exactRevision.Content.ReadFromJsonAsync<ProgramRevisionDocument>();
        Assert.Equal("Advisor A", immutableRevision?.Plan.ReadinessAdvisor);
        using var anchoredHistory = await owner.GetAsync(
            $"{programPath}/revisions?minimum_program_revision=2");
        using var futureHistory = await owner.GetAsync(
            $"{programPath}/revisions?minimum_program_revision=3");
        using var invalidHistory = await owner.GetAsync(
            $"{programPath}/revisions?minimum_program_revision=0");
        using var futureExact = await owner.GetAsync($"{programPath}/revisions/3");
        using var invalidExact = await owner.GetAsync($"{programPath}/revisions/0");
        Assert.Equal(HttpStatusCode.OK, anchoredHistory.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, futureHistory.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidHistory.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, futureExact.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidExact.StatusCode);
        var exactToolInput = new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId,
            ["program_id"] = registration.ProgramId,
            ["revision"] = 1,
        };
        await using (var ownerMcp = await McpScenario.ConnectAsync(owner,
                         new Uri(owner.BaseAddress!, "/mcp")))
        {
            _ = await ownerMcp.When("bdgrz.program.revision.get", exactToolInput).ExpectSuccess();
        }
        await using (var outsiderMcp = await McpScenario.ConnectAsync(outsider,
                         new Uri(outsider.BaseAddress!, "/mcp")))
        {
            _ = await outsiderMcp.When("bdgrz.program.revision.get", exactToolInput).ExpectFailure();
        }
        using var firstRevisionPage = await owner.GetAsync($"{programPath}/revisions?limit=1");
        var firstRevision = await firstRevisionPage.Content.ReadFromJsonAsync<ProgramRevisionPageDocument>();
        Assert.Equal(1, Assert.Single(firstRevision?.Items ?? []).Revision);
        Assert.False(string.IsNullOrWhiteSpace(firstRevision?.NextCursor));
        using var nextRevisionPage = await owner.GetAsync(
            $"{programPath}/revisions?limit=1&cursor={Uri.EscapeDataString(firstRevision.NextCursor)}");
        var nextRevision = await nextRevisionPage.Content.ReadFromJsonAsync<ProgramRevisionPageDocument>();
        Assert.Equal(2, Assert.Single(nextRevision?.Items ?? []).Revision);
        using var deniedRevisions = await outsider.GetAsync($"{programPath}/revisions");
        using var deniedExactRevision = await outsider.GetAsync(exactRevisionPath);
        Assert.Equal(HttpStatusCode.NotFound, deniedRevisions.StatusCode);
        Assert.Equal(deniedRevisions.StatusCode, deniedExactRevision.StatusCode);
        using var listed = await owner.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        var page = await listed.Content.ReadFromJsonAsync<ProgramPageDocument>();
        Assert.Single(page?.Items ?? []);

        using var secondTenantResponse = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Second Program Tenant",
            slug = $"second-program-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, secondTenantResponse.StatusCode);
        var secondTenant = await secondTenantResponse.Content
            .ReadFromJsonAsync<TenantRegistrationDocument>();
        Assert.NotNull(secondTenant);
        var secondPath = $"/api/v1/tenants/{secondTenant.TenantId}/programs";
        ProgramRegistrationDocument? secondRegistration = null;
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(secondPath,
                new { name = "Second tenant program", plan = create.plan });
            if (response.StatusCode == HttpStatusCode.OK)
            {
                secondRegistration = await response.Content
                    .ReadFromJsonAsync<ProgramRegistrationDocument>();
                break;
            }
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        Assert.NotNull(secondRegistration);
        ProgramPageDocument? firstTenantPrograms = null;
        ProgramPageDocument? secondTenantPrograms = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var firstList = await owner.GetAsync(path);
            using var secondList = await owner.GetAsync(secondPath);
            firstTenantPrograms = await firstList.Content.ReadFromJsonAsync<ProgramPageDocument>();
            secondTenantPrograms = await secondList.Content.ReadFromJsonAsync<ProgramPageDocument>();
            if (firstTenantPrograms?.Items.Count == 1 && secondTenantPrograms?.Items.Count == 1)
                break;
            await Task.Delay(250);
        }
        Assert.Equal("SOC 2 continuing program", Assert.Single(firstTenantPrograms?.Items ?? []).Name);
        Assert.Equal("Second tenant program", Assert.Single(secondTenantPrograms?.Items ?? []).Name);
        using var wrongTenantRevision = await owner.GetAsync(
            $"{secondPath}/{registration.ProgramId}/revisions/1");
        Assert.Equal(HttpStatusCode.NotFound, wrongTenantRevision.StatusCode);

        using var conflictingTenant = await owner.PostAsJsonAsync(path, new
        {
            tenant_id = secondTenant.TenantId,
            name = "Conflicting tenant body",
            plan = create.plan,
        });
        Assert.Equal(HttpStatusCode.OK, conflictingTenant.StatusCode);
        ProgramPageDocument? firstAfterConflict = null;
        ProgramPageDocument? secondAfterConflict = null;
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var firstList = await owner.GetAsync(path);
            using var secondList = await owner.GetAsync(secondPath);
            firstAfterConflict = await firstList.Content.ReadFromJsonAsync<ProgramPageDocument>();
            secondAfterConflict = await secondList.Content.ReadFromJsonAsync<ProgramPageDocument>();
            if (firstAfterConflict?.Items.Count == 2 || secondAfterConflict?.Items.Count == 2)
                break;
            await Task.Delay(250);
        }
        Assert.Equal(2, firstAfterConflict?.Items.Count);
        Assert.Single(secondAfterConflict?.Items ?? []);
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

    sealed record TenantRegistrationDocument([property: JsonPropertyName("tenant_id")] string TenantId);
    sealed record ProgramRegistrationDocument([property: JsonPropertyName("program_id")] string ProgramId);
    sealed record ClientServiceRegistrationDocument(
        [property: JsonPropertyName("service_id")] string ServiceId);
    sealed record ClientServiceDocument(string Status);
    sealed record BoundaryRegistrationDocument(
        [property: JsonPropertyName("boundary_id")] string BoundaryId,
        [property: JsonPropertyName("draft_version_id")] string DraftVersionId);
    sealed record BoundaryDocument(BoundaryDraftDocument? Draft, long Revision);
    sealed record BoundaryDraftDocument(
        [property: JsonPropertyName("version_id")] string VersionId, long Revision);
    sealed record ProgramPlanDocument([property: JsonPropertyName("readiness_advisor")] string? ReadinessAdvisor);
    sealed record ProgramDocument(string Name, string Stage,
        [property: JsonPropertyName("next_stage")] string? NextStage, long Revision,
        ProgramPlanDocument Plan,
        [property: JsonPropertyName("stage_plan")] IReadOnlyList<ProgramStageDocument> StagePlan);
    sealed record ProgramStageDocument(string Stage,
        [property: JsonPropertyName("advance_when")] string AdvanceWhen);
    sealed record ProgramPageDocument(IReadOnlyList<ProgramDocument> Items);
    sealed record ProgramRevisionPageDocument(IReadOnlyList<ProgramRevisionDocument> Items,
        [property: JsonPropertyName("next_cursor")] string? NextCursor);
    sealed record ProgramRevisionDocument(long Revision, ProgramPlanDocument Plan,
        [property: JsonPropertyName("actor_display")] string ActorDisplay);
    sealed record ProgramSetupDocument(IReadOnlyList<ProgramSetupItemDocument> Items,
        [property: JsonPropertyName("next_boundary_cursor")] string? NextBoundaryCursor);
    sealed record ProgramSetupItemDocument(string Code,
        [property: JsonPropertyName("source_id")] string? SourceId);
}
