using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class AccessReviewE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldAttestReviewRemediateAndCompleteGivenStandaloneHost()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var owner = factory.CreateClient();
        using var outsider = factory.CreateClient();
        var ownerId = await TenantInvitationE2ETests.LoginAsync(owner,
            $"access-review-owner-{Guid.NewGuid():N}@example.com");
        await TenantInvitationE2ETests.LoginAsync(outsider,
            $"access-review-outsider-{Guid.NewGuid():N}@example.com");
        using var tenant = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Access review tenant",
            slug = $"access-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, tenant.StatusCode);
        var tenantId = (await ReadAsync(tenant)).GetProperty("tenant_id").GetString()!;
        var root = $"/api/v1/tenants/{tenantId}";
        var applicationId = await PostWhenAuthorizedAsync(owner, $"{root}/applications",
            new { name = "AWS", purpose = "Production cloud" }, "application_id");
        using var declared = await owner.PostAsJsonAsync(
            $"{root}/applications/{applicationId}/system-instances",
            new { expected_application_revision = 1, name = "Production account", kind = "aws_account" });
        Assert.Equal(HttpStatusCode.OK, declared.StatusCode);
        var instanceId = (await ReadAsync(declared)).GetProperty("system_instance_id").GetString()!;
        var memberId = RbacIds.Member(Uuid.Parse(tenantId, CultureInfo.InvariantCulture),
            Uuid.Parse(ownerId, CultureInfo.InvariantCulture));

        // Act
        var populationId = await PostWhenAuthorizedAsync(owner, $"{root}/access-populations", new
        {
            application_id = applicationId,
            system_instance_id = instanceId,
            expected_system_instance_revision = 1,
            observed_at = DateTimeOffset.UtcNow.AddMinutes(-5),
            source = "IAM console export reviewed by the access owner",
        }, "population_id");
        await RecordFactsAsync(owner, root, populationId, withAdmin: true);
        var preview = await WaitForAsync(owner, $"{root}/access-populations/{populationId}/preview",
            static _ => true);
        using var accepted = await owner.PostAsJsonAsync(
            $"{root}/access-populations/{populationId}/acceptance",
            new { expected_revision = 2, attestation = "Observed population." });
        var population = await WaitForAsync(owner, $"{root}/access-populations/{populationId}",
            static _ => true);
        var listed = await WaitForAsync(owner, $"{root}/system-instances/{instanceId}/access-populations",
            static body => body.GetProperty("items").EnumerateArray()
                .Any(item => item.GetProperty("status").GetString() == "accepted"));
        var coverage = await WaitForAsync(owner,
            $"{root}/applications/{applicationId}/access-review-coverage", static _ => true);
        using var launch = await owner.PostAsJsonAsync($"{root}/access-review-campaigns", new
        {
            name = "Quarterly AWS review",
            instructions = "Revoke access nobody needs.",
            deadline = DateTimeOffset.UtcNow.AddDays(14),
            assignments = new[]
            {
                new { population_id = populationId, reviewer_member_id = memberId.ToString(),
                    delegation_reason = "No access owner is recorded yet." },
            },
        });
        var campaignId = (await ReadAsync(launch)).GetProperty("campaign_id").GetString()!;
        var campaign = await WaitForAsync(owner, $"{root}/access-review-campaigns/{campaignId}",
            static _ => true);
        var revision = 1;
        string? adminItem = null;
        foreach (var item in campaign.GetProperty("items").EnumerateArray())
        {
            var frozen = item.GetProperty("item");
            var itemId = frozen.GetProperty("item_id").GetString()!;
            var revoke = frozen.GetProperty("provider_entitlement_id").GetString() == "admin";
            adminItem = revoke ? itemId : adminItem;
            using var decided = await owner.PostAsJsonAsync(
                $"{root}/access-review-campaigns/{campaignId}/items/{itemId}/decisions",
                new
                {
                    expected_revision = revision++,
                    decision = revoke ? "revoke" : "keep",
                    rationale = "Reviewed."
                });
            Assert.Equal(HttpStatusCode.OK, decided.StatusCode);
        }
        using var blocked = await owner.PostAsJsonAsync(
            $"{root}/access-review-campaigns/{campaignId}/completion",
            new { expected_revision = revision, attestation = "Done." });
        var laterId = await PostWhenAuthorizedAsync(owner, $"{root}/access-populations", new
        {
            application_id = applicationId,
            system_instance_id = instanceId,
            expected_system_instance_revision = 1,
            observed_at = DateTimeOffset.UtcNow,
            source = "Follow-up export",
        }, "population_id");
        await RecordFactsAsync(owner, root, laterId, withAdmin: false);
        using var laterAccepted = await owner.PostAsJsonAsync(
            $"{root}/access-populations/{laterId}/acceptance",
            new { expected_revision = 2, attestation = "Observed after remediation." });
        Assert.True(laterAccepted.StatusCode == HttpStatusCode.OK,
            await laterAccepted.Content.ReadAsStringAsync());
        using var verified = await owner.PostAsJsonAsync(
            $"{root}/access-review-campaigns/{campaignId}/items/{adminItem}/remediation-verifications",
            new { expected_revision = revision++, population_id = laterId });
        Assert.True(verified.StatusCode == HttpStatusCode.OK,
            await verified.Content.ReadAsStringAsync());
        using var completed = await owner.PostAsJsonAsync(
            $"{root}/access-review-campaigns/{campaignId}/completion",
            new { expected_revision = revision, attestation = "Every decision is recorded and verified." });
        Assert.True(completed.StatusCode == HttpStatusCode.OK,
            await completed.Content.ReadAsStringAsync());
        var campaigns = await WaitForAsync(owner, $"{root}/access-review-campaigns",
            static body => body.GetProperty("items").EnumerateArray()
                .Any(item => item.GetProperty("status").GetString() == "completed"));
        using var outsiderCampaign = await outsider.GetAsync($"{root}/access-review-campaigns/{campaignId}");
        using var outsiderPopulation = await outsider.GetAsync($"{root}/access-populations/{populationId}");

        // Assert
        Assert.True(preview.GetProperty("can_accept").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("accepted", population.GetProperty("status").GetString());
        Assert.Equal(64, population.GetProperty("content_sha256").GetString()!.Length);
        Assert.Equal(populationId, listed.GetProperty("items")[0].GetProperty("population_id").GetString());
        Assert.Equal("scope_unresolved", coverage.GetProperty("instances")[0].GetProperty("coverage").GetString());
        Assert.Equal(HttpStatusCode.OK, launch.StatusCode);
        Assert.Equal(2, campaign.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, laterAccepted.StatusCode);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal(campaignId, campaigns.GetProperty("items")[0].GetProperty("campaign_id").GetString());
        Assert.Equal(HttpStatusCode.NotFound, outsiderCampaign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderPopulation.StatusCode);
        await using var mcp = await McpScenario.ConnectAsync(owner, new Uri(owner.BaseAddress!, "/mcp"));
        _ = await mcp.When("bdgrz.access_population.get", new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId,
            ["population_id"] = populationId,
        }).ExpectSuccess();
    }

    static async Task RecordFactsAsync(HttpClient owner, string root, string populationId,
        bool withAdmin)
    {
        var assignments = new List<object> { new { principal_provider_subject_id = "admins",
            provider_entitlement_id = "admin" }, new { principal_provider_subject_id = "ada",
            provider_entitlement_id = "read" } };
        using var response = await owner.PutAsJsonAsync($"{root}/access-populations/{populationId}/facts", new
        {
            expected_revision = 1,
            principals = new[]
            {
                new { provider_subject_id = "ada", principal_kind = "user_account", display_name = "Ada", status = "active" },
                new { provider_subject_id = "admins", principal_kind = "group", display_name = "Admins", status = "active" },
            },
            entitlements = new[]
            {
                new { provider_entitlement_id = "admin", entitlement_kind = "admin_privilege", display_name = "Admin" },
                new { provider_entitlement_id = "read", entitlement_kind = "permission", display_name = "Read" },
            },
            group_members = withAdmin
                ? new[] { new { group_provider_subject_id = "admins", member_provider_subject_id = "ada" } }
                : [],
            assignments,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Retries until the tenant bootstrap and permission backfills have landed.</summary>
    static async Task<string> PostWhenAuthorizedAsync(HttpClient owner, string path, object body,
        string idProperty)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(path, body);
            if (response.StatusCode == HttpStatusCode.OK)
                return (await ReadAsync(response)).GetProperty(idProperty).GetString()!;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or
                HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException($"{path} never became authorized after tenant bootstrap.");
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

    static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
        .RootElement.Clone();
}
