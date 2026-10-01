using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class PolicyDistributionE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    static readonly string Today = DateTime.UtcNow.ToString("yyyy-MM-dd",
        CultureInfo.InvariantCulture);

    [Fact]
    public async Task ShouldGovernPolicyDraftAndReconcileTrainingCampaignGivenStandaloneHost()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var owner = factory.CreateClient();
        using var outsider = factory.CreateClient();
        await TenantInvitationE2ETests.LoginAsync(owner,
            $"policy-owner-{Guid.NewGuid():N}@example.com");
        await TenantInvitationE2ETests.LoginAsync(outsider,
            $"policy-outsider-{Guid.NewGuid():N}@example.com");
        var (tenantId, programId) = await CreateProgramAsync(owner);
        var root = $"/api/v1/tenants/{tenantId}";
        var program = $"{root}/programs/{programId}";
        var ada = await RecordWorkerAsync(owner, root, "Ada Lovelace", "E-1", "employee",
            "Engineering");
        await RecordWorkerAsync(owner, root, "Bob Contractor", "C-1", "contractor", "Sales");
        var firstRoster = await FreezeRosterAsync(owner, root);

        // Act
        using var created = await owner.PostAsJsonAsync($"{program}/policies", new
        {
            identifier = "pol-ac",
            content = Content("Access Control Policy"),
        });
        var policyId = (await ReadAsync(created)).GetProperty("policy_id").GetString();
        var policyPath = $"{program}/policies/{policyId}";
        using var selfReview = await owner.PostAsJsonAsync($"{policyPath}/reviews",
            new { expected_revision = 1, outcome = "accept", rationale = "Mine" });
        using var outsiderRead = await outsider.GetAsync(policyPath);
        var (otherTenantId, otherProgramId) = await CreateProgramAsync(owner);
        using var crossTenant = await owner.GetAsync(
            $"/api/v1/tenants/{otherTenantId}/programs/{otherProgramId}/policies/{policyId}");
        await using var mcp = await McpScenario.ConnectAsync(owner,
            new Uri(owner.BaseAddress!, "/mcp"));
        _ = await mcp.When("bdgrz.policy.draft.revise", new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId,
            ["program_id"] = programId,
            ["policy_id"] = policyId,
            ["expected_revision"] = 1,
            ["content"] = Content("Access Control Policy (revised)"),
        }).ExpectSuccess();
        using var policy = await owner.GetAsync(policyPath);
        var policies = await WaitForAsync(owner, $"{program}/policies",
            static body => Items(body).Any(item => item.GetProperty("revision").GetInt64() == 2));
        var requirement = await mcp.When("bdgrz.training_requirement.define",
            new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["program_id"] = programId,
                ["identifier"] = "SAT",
                ["content"] = new Dictionary<string, object?>
                {
                    ["course_name"] = "Security awareness",
                    ["audience_kind"] = "core_security",
                    ["delivery_source"] = "lms_export",
                },
            }).ExpectSuccess();
        var requirementId = Assert.IsType<JsonElement>(requirement.StructuredJson)
            .GetProperty("result").GetProperty("requirement_id").GetString();
        using var launched = await owner.PostAsJsonAsync($"{program}/training-campaigns", new
        {
            requirement_id = requirementId,
            requirement_version = 1,
            roster_snapshot_id = firstRoster,
            due_on = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd",
                CultureInfo.InvariantCulture),
        });
        var launchBody = await ReadAsync(launched);
        var campaignId = launchBody.GetProperty("campaign_id").GetString();
        var campaignPath = $"{program}/campaigns/{campaignId}";
        _ = await mcp.When("bdgrz.training_completion.record", new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId,
            ["program_id"] = programId,
            ["campaign_id"] = campaignId,
            ["person_id"] = ada,
            ["requirement_version"] = 1,
            ["completed_on"] = Today,
            ["source"] = "lms_export",
            ["evidence_reference"] = "lms-export.csv#2",
        }).ExpectSuccess();
        using var wrongVersion = await owner.PostAsJsonAsync($"{campaignPath}/completions", new
        {
            person_id = ada,
            requirement_version = 2,
            completed_on = Today,
            source = "manual",
            evidence_reference = "certificate.pdf",
        });
        var cy = await RecordWorkerAsync(owner, root, "Cy Joiner", "E-2", "employee",
            "Engineering");
        var secondRoster = await FreezeRosterAsync(owner, root);
        using var reconciled = await owner.PostAsJsonAsync($"{campaignPath}/reconciliations",
            new { roster_snapshot_id = secondRoster });
        using var campaign = await owner.GetAsync(campaignPath);
        var campaigns = await WaitForAsync(owner, $"{program}/campaigns",
            static body => Items(body).Any());
        using var outsiderCampaign = await outsider.GetAsync(campaignPath);

        // Assert
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, selfReview.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderRead.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);
        var policyBody = await ReadAsync(policy);
        Assert.Equal(2, policyBody.GetProperty("revision").GetInt64());
        Assert.Equal("draft", policyBody.GetProperty("status").GetString());
        Assert.Equal("POL-AC", Items(policies).Single().GetProperty("identifier").GetString());
        Assert.Equal(HttpStatusCode.OK, launched.StatusCode);
        Assert.Equal(2, launchBody.GetProperty("audience_count").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, wrongVersion.StatusCode);
        var reconciliation = await ReadAsync(reconciled);
        var amendment = Assert.Single(reconciliation.GetProperty("amendments").EnumerateArray());
        Assert.Equal(cy, amendment.GetProperty("person_id").GetString());
        Assert.Equal("joiner", amendment.GetProperty("reason").GetString());
        var totals = (await ReadAsync(campaign)).GetProperty("totals");
        Assert.Equal(3, totals.GetProperty("population").GetInt32());
        Assert.Equal(1, totals.GetProperty("satisfied").GetInt32());
        Assert.Equal(campaignId, Items(campaigns).Single().GetProperty("campaign_id").GetString());
        Assert.Equal(HttpStatusCode.NotFound, outsiderCampaign.StatusCode);
    }

    [Fact]
    public async Task ShouldReturnTransientListLagAndRecoverGivenSplitWorkerRestart()
    {
        // Arrange
        var applicationName = $"compliance-policy-split-{Guid.NewGuid():N}";
        using var worker = BuildWorker(applicationName);
        await worker.StartAsync();
        await using var factory = E2EAppFactory.Create(broker, applicationName);
        var priorMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
        HttpClient client;
        try
        {
            Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", "api");
            client = factory.CreateClient();
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", priorMode);
        }
        using var owner = client;
        await TenantInvitationE2ETests.LoginAsync(owner,
            $"policy-split-{Guid.NewGuid():N}@example.com");
        var (tenantId, programId) = await CreateProgramAsync(owner);
        var program = $"/api/v1/tenants/{tenantId}/programs/{programId}";
        _ = await WaitForAsync(owner, $"{program}/policies", static _ => true);
        await worker.StopAsync();

        // Act
        using var created = await owner.PostAsJsonAsync($"{program}/policies", new
        {
            identifier = "POL-SPLIT",
            content = Content("Split host policy"),
        });
        var policyId = (await ReadAsync(created)).GetProperty("policy_id").GetString();
        using var source = await owner.GetAsync($"{program}/policies/{policyId}");
        using var lagged = await owner.GetAsync($"{program}/policies");
        using var restarted = BuildWorker(applicationName);
        await restarted.StartAsync();
        try
        {
            var recovered = await WaitForAsync(owner, $"{program}/policies",
                static body => Items(body).Any());

            // Assert
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            Assert.Equal(HttpStatusCode.OK, source.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, lagged.StatusCode);
            Assert.Equal("true", lagged.Headers.GetValues("Portia-Transient").Single());
            Assert.Equal(policyId, Items(recovered).Single().GetProperty("policy_id").GetString());
        }
        finally
        {
            await restarted.StopAsync();
        }
    }

    static Dictionary<string, object?> Content(string title) => new()
    {
        ["title"] = title,
        ["purpose"] = "Govern access to in-scope systems",
        ["audience_kind"] = "core_security",
        ["review_cadence_months"] = 12,
        ["body"] = "Access is granted by least privilege and reviewed quarterly.",
        ["owner_reference"] = "Security lead",
        ["applicability"] = new[]
        {
            new Dictionary<string, object?>
            {
                ["subject_type"] = "process",
                ["subject"] = "Quarterly access review",
            },
        },
    };

    static JsonElement.ArrayEnumerator Items(JsonElement page) =>
        page.GetProperty("items").EnumerateArray();

    static async Task<(Guid TenantId, Guid ProgramId)> CreateProgramAsync(HttpClient owner)
    {
        using var tenant = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Policy tenant",
            slug = $"policy-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, tenant.StatusCode);
        var tenantId = Guid.Parse((await ReadAsync(tenant)).GetProperty("tenant_id").GetString()!);
        await AccessGrantE2ESupport.IssueFounderOrganizationGrantAsync(owner, tenantId);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var program = await owner.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/programs",
                new
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
            if (program.StatusCode == HttpStatusCode.OK)
                return (tenantId, Guid.Parse((await ReadAsync(program)).GetProperty("program_id")
                    .GetString()!));
            Assert.True(program.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await program.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("Program creation never became authorized after tenant bootstrap.");
    }

    /// <summary>Records a person and work relationship once workforce access has been granted.</summary>
    static async Task<string> RecordWorkerAsync(HttpClient owner, string root, string name,
        string workerId, string workerType, string department)
    {
        string? personId = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (personId is null && DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync($"{root}/people",
                new { display_name = name });
            if (response.StatusCode == HttpStatusCode.OK)
                personId = (await ReadAsync(response)).GetProperty("person_id").GetString();
            else
            {
                Assert.True(response.StatusCode is HttpStatusCode.NotFound or
                    HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
                await Task.Delay(250);
            }
        }
        Assert.NotNull(personId);
        using var job = await owner.PostAsJsonAsync($"{root}/work-relationships", new
        {
            person_id = personId,
            source_worker_id = workerId,
            worker_type = workerType,
            lifecycle_status = "active",
            start_date = Today,
            department,
        });
        Assert.Equal(HttpStatusCode.OK, job.StatusCode);
        var relationshipId = (await ReadAsync(job)).GetProperty("relationship_id").GetString();
        _ = await WaitForAsync(owner, $"{root}/people/{personId}?minimum_revision=1",
            static _ => true);
        _ = await WaitForAsync(owner,
            $"{root}/work-relationships/{relationshipId}?minimum_revision=1", static _ => true);
        return personId;
    }

    /// <summary>Freezes the roster once the workforce projections have reached their sources.</summary>
    static async Task<string> FreezeRosterAsync(HttpClient owner, string root)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync($"{root}/workforce-roster-snapshots",
                new { });
            if (response.StatusCode == HttpStatusCode.OK)
                return (await ReadAsync(response)).GetProperty("snapshot_id").GetString()!;
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await Task.Delay(250);
        }
        throw new TimeoutException("The workforce roster never froze.");
    }

    /// <summary>Retries transient projection lag until the body satisfies the condition.</summary>
    static async Task<JsonElement> WaitForAsync(HttpClient client, string path,
        Func<JsonElement, bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var body = await ReadAsync(response);
                if (condition(body))
                    return body;
            }
            else
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await Task.Delay(250);
        }
        throw new TimeoutException($"{path} did not reach the expected state.");
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

    static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
        .RootElement.Clone();
}
