using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ProviderChangeImpactE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    static readonly string[] ExpectedImpactContexts = ["controls", "data", "evidence", "scope", "systems"];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldAssessLinkedRecordsWithoutApprovingGivenProviderChangeInStandaloneOrSplitHost(
        bool split)
    {
        // Arrange
        var applicationName = $"compliance-provider-impact-{Guid.NewGuid():N}";
        using var worker = split ? BuildWorker(applicationName) : null;
        if (worker is not null)
            await worker.StartAsync();
        await using var factory = E2EAppFactory.Create(broker, applicationName);
        var previousMode = TestHostMode.Current;
        HttpClient ownerClient;
        HttpClient outsiderClient;
        try
        {
            TestHostMode.Set(split ? "api" : "standalone");
            ownerClient = factory.CreateClient();
            outsiderClient = factory.CreateClient();
        }
        finally
        {
            TestHostMode.Set(previousMode);
        }
        using var owner = ownerClient;
        using var outsider = outsiderClient;
        var ownerUser = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(owner,
            $"provider-impact-{Guid.NewGuid():N}@example.com"), CultureInfo.InvariantCulture);
        await TenantInvitationE2ETests.LoginAsync(outsider,
            $"provider-impact-outsider-{Guid.NewGuid():N}@example.com");
        var tenantId = await CreateTenantAsync(owner);
        await AccessGrantE2ESupport.IssueFounderOrganizationGrantAsync(owner, tenantId);
        var tenantPath = $"/api/v1/tenants/{tenantId}";
        var personId = await PostIdAsync(owner, $"{tenantPath}/people",
            new { display_name = "Provider inventory owner" }, "person_id");
        var programId = await CreateProgramAsync(owner, tenantId);
        var applicationId = await PostIdAsync(owner, $"{tenantPath}/applications",
            new { name = "Payroll", purpose = "Run payroll" }, "application_id");
        var applicationPath = $"{tenantPath}/applications/{applicationId}";
        var systemInstanceId = await PostIdAsync(owner, $"{applicationPath}/system-instances",
            new
            {
                expected_application_revision = 1,
                name = "Payroll production",
                kind = "production",
                source_identifier = "payroll-prod",
            }, "system_instance_id");
        var informationAssetId = await PostIdAsync(owner, $"{tenantPath}/information-assets",
            new
            {
                name = "Payroll records",
                classification = "confidential",
                retention_reference = "Payroll retention schedule",
                owner_person_id = personId,
                description = "Employee payroll data",
            }, "information_asset_id");
        var componentId = await PostIdAsync(owner, $"{tenantPath}/technology-components",
            new
            {
                category = "cloud_account",
                name = "Payroll production account",
                owner_person_id = personId,
                system_instance_id = systemInstanceId,
            }, "component_id");
        var firstFlowId = await RecordFlowAsync(owner, tenantPath, systemInstanceId,
            informationAssetId, personId, "Payroll processing");
        var controlId = await PostIdAsync(owner,
            $"{tenantPath}/programs/{programId}/controls", new
            {
                identifier = "AC-PROVIDER-IMPACT",
                content = ControlContent(applicationId),
            }, "control_id");
        var ownerMemberId = RbacIds.Member(Uuid.Parse(tenantId.ToString(),
            CultureInfo.InvariantCulture), ownerUser);
        await PostWithoutResultAsync(owner, $"{tenantPath}/programs/{programId}/evidence-requests",
            new
            {
                title = "Payroll access review",
                instructions = "Attach the current access review record.",
                owner_member_id = ownerMemberId,
                due_on = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
                control_id = controlId,
            });
        var providerId = await RecordProviderAsync(owner, tenantPath, applicationId,
            systemInstanceId);
        var previewPath = $"{tenantPath}/providers/{providerId}/change-impact-previews";
        var previewRequest = new
        {
            expected_revision = 1,
            change_kind = "material_change",
            effective_on = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd",
                CultureInfo.InvariantCulture),
            change_summary = "The supplier is changing its production hosting region.",
        };
        using var restartedWorker = split ? BuildWorker(applicationName) : null;

        // Act
        using var preview = await PreviewWhenCaughtUpAsync(owner, previewPath, previewRequest);
        var previewDocument = preview.RootElement;
        if (worker is not null)
        {
            await worker.StopAsync();
            var recoveredFlowId = await RecordFlowAsync(owner, tenantPath, systemInstanceId,
                informationAssetId, personId, "Payroll processing after region change");
            using var lagging = await owner.PostAsJsonAsync(previewPath, previewRequest);
            Assert.Equal(HttpStatusCode.Conflict, lagging.StatusCode);
            Assert.Equal("true", lagging.Headers.GetValues("Portia-Transient").Single());
            await restartedWorker!.StartAsync();
            using var recovered = await PreviewWhenCaughtUpAsync(owner, previewPath,
                previewRequest);
            AssertImpact(recovered.RootElement, applicationId, systemInstanceId,
                componentId, informationAssetId, recoveredFlowId, controlId);
            using var replay = await owner.PostAsJsonAsync(previewPath, previewRequest);
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            using var replayBody = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
            Assert.Equal(recovered.RootElement.GetProperty("digest").GetString(),
                replayBody.RootElement.GetProperty("digest").GetString());
        }

        using var stale = await owner.PostAsJsonAsync(previewPath, new
        {
            expected_revision = 2,
            change_kind = "renewal",
            effective_on = previewRequest.effective_on,
            change_summary = "The current provider revision is stale.",
        });
        var otherTenantId = await CreateTenantAsync(owner);
        await AccessGrantE2ESupport.IssueFounderOrganizationGrantAsync(owner, otherTenantId);
        using var foreign = await owner.PostAsJsonAsync(
            $"/api/v1/tenants/{otherTenantId}/providers/{providerId}/change-impact-previews",
            previewRequest);
        using var unauthorized = await outsider.PostAsJsonAsync(previewPath, previewRequest);

        // Assert
        AssertImpact(previewDocument, applicationId, systemInstanceId,
            componentId, informationAssetId, firstFlowId, controlId);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.False(stale.Headers.TryGetValues("Portia-Transient", out var staleTransient) &&
                     staleTransient.Contains("true", StringComparer.Ordinal));
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.True(unauthorized.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound);
        using var provider = await owner.GetAsync($"{tenantPath}/providers/{providerId}");
        Assert.Equal(HttpStatusCode.OK, provider.StatusCode);
        using var providerBody = JsonDocument.Parse(await provider.Content.ReadAsStringAsync());
        Assert.Equal(1, providerBody.RootElement.GetProperty("revision").GetInt64());
        if (restartedWorker is not null)
            await restartedWorker.StopAsync();
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

    static async Task<Guid> CreateTenantAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Provider impact tenant",
            slug = $"impact-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TenantDocument>())!.TenantId;
    }

    static async Task<Guid> CreateProgramAsync(HttpClient client, Guid tenantId)
    {
        await AccessGrantE2ESupport.IssueFounderOrganizationGrantAsync(client, tenantId);
        var path = $"/api/v1/tenants/{tenantId}/programs";
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.PostAsJsonAsync(path, new
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
                return (await response.Content.ReadFromJsonAsync<ProgramRegistrationDocument>())!
                    .ProgramId;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("Program creation remained unauthorized after bootstrap.");
    }

    static async Task<Guid> PostIdAsync(HttpClient client, string path, object body,
        string property)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.PostAsJsonAsync(path, body);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                return document.RootElement.GetProperty(property).GetGuid();
            }
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException($"POST {path} remained unauthorized after bootstrap.");
    }

    static async Task PostWithoutResultAsync(HttpClient client, string path, object body)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.PostAsJsonAsync(path, body);
            if (response.StatusCode == HttpStatusCode.OK)
                return;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException($"POST {path} remained unauthorized after bootstrap.");
    }

    static async Task<Guid> RecordFlowAsync(HttpClient client, string tenantPath,
        Guid systemInstanceId, Guid informationAssetId, Guid ownerPersonId, string purpose) =>
        await PostIdAsync(client, $"{tenantPath}/data-flows", new
        {
            source_type = "system_instance",
            source_id = systemInstanceId,
            destination_type = "external_party",
            information_asset_ids = new[] { informationAssetId },
            purpose,
            encrypted_in_transit = true,
            encrypted_at_rest = true,
            effective_from = DateOnly.FromDateTime(DateTime.UtcNow),
            owner_person_id = ownerPersonId,
            destination_party = "Payroll processor",
        }, "data_flow_id");

    static async Task<Guid> RecordProviderAsync(HttpClient client, string tenantPath,
        Guid applicationId, Guid systemInstanceId) =>
        await PostIdAsync(client, $"{tenantPath}/providers", new
        {
            content = new
            {
                name = "Payroll hosting supplier",
                provider_kind = "vendor",
                dependencies = new[]
                {
                    new
                    {
                        subject_kind = "system_instance",
                        subject_id = systemInstanceId,
                        application_id = applicationId,
                        rationale = "Hosts the production payroll system.",
                        effective_from = "2026-01-01T00:00:00Z",
                    },
                },
            },
        }, "provider_id");

    static async Task<JsonDocument> PreviewWhenCaughtUpAsync(HttpClient client, string path,
        object request)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.PostAsJsonAsync(path, request);
            if (response.StatusCode == HttpStatusCode.OK)
                return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(response.StatusCode == HttpStatusCode.Conflict &&
                        response.Headers.TryGetValues("Portia-Transient", out var transient) &&
                        transient.Single() == "true",
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("Provider impact preview did not catch up with source projections.");
    }

    static object ControlContent(Guid applicationId) => new
    {
        title = "Review payroll access",
        objective = "Ensure access to payroll is reviewed",
        description = "Payroll access is reviewed before changes are approved.",
        implementation_narrative = "The compliance lead reviews the access listing.",
        expected_evidence_descriptions = new[] { "Payroll access review record" },
        applicability = new[]
        {
            new
            {
                entry_id = Guid.NewGuid(),
                subject_type = "application",
                subject = "Payroll",
                governed_record_id = applicationId,
                rationale = "The control applies to payroll.",
                unresolved = false,
            },
        },
    };

    static void AssertImpact(JsonElement root, Guid applicationId, Guid systemInstanceId,
        Guid componentId, Guid informationAssetId, Guid dataFlowId, Guid controlId)
    {
        Assert.True(root.GetProperty("complete").GetBoolean());
        Assert.Empty(root.GetProperty("pending_contexts").EnumerateArray());
        var contexts = root.GetProperty("contexts").EnumerateArray()
            .ToDictionary(static context => context.GetProperty("context").GetString()!,
                StringComparer.Ordinal);
        Assert.Equal(ExpectedImpactContexts,
            contexts.Keys.Order(StringComparer.Ordinal));
        AssertRecord(contexts["systems"], "application", applicationId);
        AssertRecord(contexts["systems"], "system_instance", systemInstanceId);
        AssertRecord(contexts["data"], "technology_component", componentId);
        AssertRecord(contexts["data"], "information_asset", informationAssetId);
        AssertRecord(contexts["data"], "data_flow", dataFlowId);
        AssertRecord(contexts["controls"], "control", controlId);
        AssertRecord(contexts["evidence"], "evidence_request", null);
        AssertRecord(contexts["scope"], "application", applicationId);
        AssertRecord(contexts["scope"], "system_instance", systemInstanceId);
    }

    static void AssertRecord(JsonElement context, string recordType, Guid? recordId)
    {
        Assert.True(context.GetProperty("complete").GetBoolean());
        Assert.Contains(context.GetProperty("records").EnumerateArray(), record =>
            record.GetProperty("record_type").GetString() == recordType &&
            (recordId is null || record.GetProperty("record_id").GetGuid() == recordId));
    }

    sealed record TenantDocument([property: JsonPropertyName("tenant_id")] Guid TenantId);
    sealed record ProgramRegistrationDocument(
        [property: JsonPropertyName("program_id")] Guid ProgramId);
}
