using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class WorkforceServiceIdentityE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task ShouldGovernExpiryProvenanceAndAccountCorrelationGivenStandaloneHost()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var owner = factory.CreateClient();
        using var other = factory.CreateClient();
        await TenantInvitationE2ETests.LoginAsync(owner, $"nhi-owner-{Guid.NewGuid():N}@example.com");
        await TenantInvitationE2ETests.LoginAsync(other, $"nhi-other-{Guid.NewGuid():N}@example.com");
        var root = await CreateTenantRootAsync(owner, "NHI tenant");
        var otherRoot = await CreateTenantRootAsync(other, "Other NHI tenant");
        var personId = await PostWhenAuthorizedAsync(owner, $"{root}/people",
            new { display_name = "Ada Lovelace" }, "person_id");
        var identityId = await PostWhenAuthorizedAsync(owner, $"{root}/service-identities",
            Terms(personId, Today.AddDays(60)), "service_identity_id");
        var identityPath = $"{root}/service-identities/{identityId}";
        var recorded = await WaitAsync(owner, $"{identityPath}?minimum_revision=1", static _ => true);

        // Act
        using var past = await owner.PostAsJsonAsync($"{root}/service-identities",
            Terms(personId, Today.AddDays(-1)));
        using var source = await owner.PostAsJsonAsync($"{root}/workforce-source-observations", new
        {
            source = new { source_kind = "provider", source_system = "github", source_record_id = "bot-100", source_revision = "v1" },
            target_kind = "service_identity",
            target_id = identityId,
            expected_target_revision = 1,
            observed_at = DateTimeOffset.UtcNow.AddDays(-1),
            facts = new
            {
                service_identity = new
                {
                    display_name = "Deploy bot",
                    identity_kind = "bot",
                    environment = "production",
                    lifecycle_status = "active",
                    expires_on = Today.AddDays(60),
                },
            },
        });
        var observationId = (await ReadAsync(source)).GetProperty("observation_id").GetString();
        var sourcePath = $"{root}/workforce-source-observations/{observationId}";
        _ = await WaitAsync(owner, $"{sourcePath}?minimum_revision=1", static _ => true);
        using var previewResponse = await owner.GetAsync($"{sourcePath}/preview");
        var preview = await ReadAsync(previewResponse);
        using var accepted = await owner.PutAsJsonAsync($"{sourcePath}/decision", new
        {
            expected_revision = 1,
            expected_target_revision = 1,
            outcome = "accepted",
            note = "Provider facts checked",
        });
        var reconciled = await WaitAsync(owner, $"{sourcePath}?minimum_revision=2", static _ => true);
        using var pastRevision = await owner.PutAsJsonAsync(identityPath, Revision(personId, 1, Today.AddDays(-1)));
        using var revised = await owner.PutAsJsonAsync(identityPath, Revision(personId, 1, Today.AddDays(1)));
        var shortened = await WaitAsync(owner, $"{identityPath}?minimum_revision=2", static _ => true);
        var populationId = await OpenAcceptedPopulationAsync(owner, root);
        using var classified = await owner.PostAsJsonAsync(
            $"{root}/access-populations/{populationId}/classifications", Classification(identityId));
        var principals = await WaitAsync(owner, $"{root}/access-populations/{populationId}/principals",
            static body => body.GetProperty("items").EnumerateArray().Any(item => item.GetProperty("classification").GetString() == "nhi"));
        using var unknownIdentity = await owner.PostAsJsonAsync(
            $"{root}/access-populations/{populationId}/classifications", Classification(Guid.NewGuid().ToString()));
        using var otherGet = await other.GetAsync(identityPath);
        using var otherList = await other.GetAsync($"{root}/service-identities");
        using var otherSource = await other.GetAsync(sourcePath);
        using var otherPrincipals = await other.GetAsync($"{root}/access-populations/{populationId}/principals");
        using var otherCrossPath = await other.GetAsync($"{otherRoot}/service-identities/{identityId}");
        using var otherCrossSource = await other.GetAsync($"{otherRoot}/workforce-source-observations/{observationId}");
        var otherOwn = await WaitAsync(other, $"{otherRoot}/service-identities", static _ => true);
        await using var mcp = await McpScenario.ConnectAsync(owner, new Uri(owner.BaseAddress!, "/mcp"));
        _ = await mcp.When("bdgrz.workforce.source.preview", new Dictionary<string, object?>
        {
            ["tenant_id"] = root.Split('/')[4],
            ["observation_id"] = observationId,
        }).ExpectSuccess();

        // Assert
        Assert.False(recorded.GetProperty("expired").GetBoolean());
        Assert.Equal(Today.AddDays(60).ToString("O", CultureInfo.InvariantCulture), recorded.GetProperty("expires_on").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        Assert.Equal("corroborating", preview.GetProperty("source_authority").GetString());
        Assert.True(preview.GetProperty("can_accept").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        Assert.True(reconciled.GetProperty("accepted_for_current_revision").GetBoolean());
        Assert.Equal("provider", reconciled.GetProperty("source").GetProperty("source_kind").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, pastRevision.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, revised.StatusCode);
        Assert.Equal(Today.AddDays(1).ToString("O", CultureInfo.InvariantCulture), shortened.GetProperty("expires_on").GetString());
        Assert.False(shortened.GetProperty("expired").GetBoolean());
        Assert.Equal(personId, shortened.GetProperty("owner_id").GetString());
        Assert.Equal("Approved deploy purpose", shortened.GetProperty("purpose").GetString());
        Assert.Equal(HttpStatusCode.OK, classified.StatusCode);
        var correlated = principals.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("provider_subject_id").GetString() == "deploy-bot");
        Assert.Equal(identityId, correlated.GetProperty("current").GetProperty("service_identity_id").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, unknownIdentity.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherGet.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherList.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherSource.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherPrincipals.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherCrossPath.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherCrossSource.StatusCode);
        Assert.Empty(otherOwn.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task ShouldRecoverNhiExpiryAndCorrelationGivenSplitWorkerRestart()
    {
        // Arrange
        var applicationName = $"compliance-nhi-split-{Guid.NewGuid():N}";
        using var worker = BuildWorker(applicationName);
        await worker.StartAsync();
        await using var factory = E2EAppFactory.Create(broker, applicationName);
        var previousMode = TestHostMode.Current;
        HttpClient client;
        try
        {
            TestHostMode.Set("api");
            client = factory.CreateClient();
        }
        finally
        {
            TestHostMode.Set(previousMode);
        }
        using var owner = client;
        await TenantInvitationE2ETests.LoginAsync(owner, $"nhi-split-{Guid.NewGuid():N}@example.com");
        var root = await CreateTenantRootAsync(owner, "NHI split tenant");
        var personId = await PostWhenAuthorizedAsync(owner, $"{root}/people",
            new { display_name = "Ada Lovelace" }, "person_id");
        var identityId = await PostWhenAuthorizedAsync(owner, $"{root}/service-identities",
            Terms(personId, Today.AddDays(30)), "service_identity_id");
        var identityPath = $"{root}/service-identities/{identityId}";
        _ = await WaitAsync(owner, $"{identityPath}?minimum_revision=1", static _ => true);
        var populationId = await OpenAcceptedPopulationAsync(owner, root);
        await worker.StopAsync();

        // Act
        using var revised = await owner.PutAsJsonAsync(identityPath, Revision(personId, 1, Today.AddDays(1)));
        using var lagged = await owner.GetAsync($"{identityPath}?minimum_revision=2");
        using var classified = await owner.PostAsJsonAsync(
            $"{root}/access-populations/{populationId}/classifications", Classification(identityId));
        using var restarted = BuildWorker(applicationName);
        await restarted.StartAsync();
        try
        {
            var shortened = await WaitAsync(owner, $"{identityPath}?minimum_revision=2", static _ => true);
            var principals = await WaitAsync(owner, $"{root}/access-populations/{populationId}/principals",
                static body => body.GetProperty("items").EnumerateArray().Any(item => item.GetProperty("classification").GetString() == "nhi"));

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, revised.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, lagged.StatusCode);
            Assert.Equal("true", lagged.Headers.GetValues("Portia-Transient").Single());
            Assert.Equal(HttpStatusCode.OK, classified.StatusCode);
            Assert.Equal(Today.AddDays(1).ToString("O", CultureInfo.InvariantCulture), shortened.GetProperty("expires_on").GetString());
            Assert.Equal(identityId, principals.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("provider_subject_id").GetString() == "deploy-bot")
                .GetProperty("current").GetProperty("service_identity_id").GetString());
        }
        finally
        {
            await restarted.StopAsync();
        }
    }

    static object Terms(string personId, DateOnly expiresOn) => new
    {
        display_name = "Deploy bot",
        identity_kind = "bot",
        purpose = "Approved deploy purpose",
        owner_kind = "person",
        owner_id = personId,
        review_by = Today.AddDays(90),
        environment = "production",
        expires_on = expiresOn,
    };

    static object Revision(string personId, long revision, DateOnly expiresOn) => new
    {
        expected_revision = revision,
        display_name = "Deploy bot",
        identity_kind = "bot",
        purpose = "Approved deploy purpose",
        owner_kind = "person",
        owner_id = personId,
        review_by = Today.AddDays(90),
        lifecycle_status = "active",
        environment = "production",
        expires_on = expiresOn,
    };

    static object Classification(string identityId) => new
    {
        provider_subject_id = "deploy-bot",
        expected_classification_count = 0,
        classification = "nhi",
        rationale = "Deploy automation governed as a service identity.",
        service_identity_id = identityId,
    };

    static async Task<string> OpenAcceptedPopulationAsync(HttpClient owner, string root)
    {
        var applicationId = await PostWhenAuthorizedAsync(owner, $"{root}/applications",
            new { name = "GitHub", purpose = "Source hosting" }, "application_id");
        using var declared = await owner.PostAsJsonAsync($"{root}/applications/{applicationId}/system-instances",
            new { expected_application_revision = 1, name = "Organization", kind = "aws_account" });
        Assert.Equal(HttpStatusCode.OK, declared.StatusCode);
        var instanceId = (await ReadAsync(declared)).GetProperty("system_instance_id").GetString();
        var populationId = await PostWhenAuthorizedAsync(owner, $"{root}/access-populations", new
        {
            application_id = applicationId,
            system_instance_id = instanceId,
            expected_system_instance_revision = 1,
            observed_at = DateTimeOffset.UtcNow.AddMinutes(-5),
            source = "Organization export reviewed by the access owner",
        }, "population_id");
        using var facts = await owner.PutAsJsonAsync($"{root}/access-populations/{populationId}/facts", new
        {
            expected_revision = 1,
            principals = new[]
            {
                new { provider_subject_id = "deploy-bot", principal_kind = "app_installation", display_name = "Deploy bot", status = "active" },
            },
            entitlements = new[]
            {
                new { provider_entitlement_id = "write", entitlement_kind = "permission", display_name = "Write" },
            },
            group_members = Array.Empty<object>(),
            assignments = new[] { new { principal_provider_subject_id = "deploy-bot", provider_entitlement_id = "write" } },
        });
        Assert.Equal(HttpStatusCode.OK, facts.StatusCode);
        _ = await WaitAsync(owner, $"{root}/access-populations/{populationId}/preview", static _ => true);
        using var accepted = await owner.PostAsJsonAsync($"{root}/access-populations/{populationId}/acceptance",
            new { expected_revision = 2, attestation = "Observed population." });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        _ = await WaitAsync(owner, $"{root}/access-populations/{populationId}/principals", static _ => true);
        return populationId;
    }

    static async Task<string> CreateTenantRootAsync(HttpClient client, string name)
    {
        using var created = await client.PostAsJsonAsync("/api/v1/tenants",
            new { name, slug = $"nhi-{Guid.NewGuid():N}"[..24] });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        return $"/api/v1/tenants/{(await ReadAsync(created)).GetProperty("tenant_id").GetString()}";
    }

    /// <summary>Retries until the tenant bootstrap and permission backfills have landed.</summary>
    static async Task<string> PostWhenAuthorizedAsync(HttpClient client, string path, object body,
        string idProperty)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.PostAsJsonAsync(path, body);
            if (response.StatusCode == HttpStatusCode.OK)
                return (await ReadAsync(response)).GetProperty(idProperty).GetString()!;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or
                HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException($"{path} never became authorized after tenant bootstrap.");
    }

    static async Task<JsonElement> WaitAsync(HttpClient client, string path, Func<JsonElement, bool> condition)
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
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Development" });
        builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
        return builder.Build();
    }

    static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync())).RootElement.Clone();
}
