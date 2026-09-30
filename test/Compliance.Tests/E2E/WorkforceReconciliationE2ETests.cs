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
public sealed class WorkforceReconciliationE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldCorrelateReconcileResolveAndFreezeGivenStandaloneHost()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var owner = factory.CreateClient();
        using var outsider = factory.CreateClient();
        await TenantInvitationE2ETests.LoginAsync(owner,
            $"reconcile-owner-{Guid.NewGuid():N}@example.com");
        await TenantInvitationE2ETests.LoginAsync(outsider,
            $"reconcile-outsider-{Guid.NewGuid():N}@example.com");
        var tenantId = await CreateTenantAsync(owner);
        var root = $"/api/v1/tenants/{tenantId}";
        var adaId = await RecordWhenAuthorizedAsync(owner, $"{root}/people",
            new { display_name = "Ada Lovelace" });
        using var graceCreated = await owner.PostAsJsonAsync($"{root}/people",
            new { display_name = "Grace Hopper" });
        var graceId = (await ReadAsync(graceCreated)).GetProperty("person_id").GetString();
        _ = await WaitForOkAsync(owner, $"{root}/people/{adaId}?minimum_revision=1");
        _ = await WaitForOkAsync(owner, $"{root}/people/{graceId}?minimum_revision=1");

        // Act
        var initial = await WaitForOkAsync(owner, $"{root}/workforce-reconciliation-observations");
        var accessOnly = Items(initial).Single(item => Kind(item) == "access_only");
        var ownerUserId = accessOnly.GetProperty("user_ids")[0].GetString();
        using var correlated = await owner.PutAsJsonAsync(
            $"{root}/people/{adaId}/membership-correlation",
            new { expected_revision = 1, user_id = ownerUserId });
        using var unknownMember = await owner.PutAsJsonAsync(
            $"{root}/people/{graceId}/membership-correlation",
            new { expected_revision = 1, user_id = Guid.NewGuid() });
        var linked = await WaitForOkAsync(owner, $"{root}/people/{adaId}?minimum_revision=2");
        using var job = await owner.PostAsJsonAsync($"{root}/work-relationships", new
        {
            person_id = adaId,
            source_worker_id = "E-100",
            worker_type = "employee",
            lifecycle_status = "active",
            start_date = "2025-01-06",
            manager_person_id = graceId,
        });
        var relationshipId = (await ReadAsync(job)).GetProperty("relationship_id").GetString();
        var relationship = await WaitForAsync(owner,
            $"{root}/work-relationships/{relationshipId}?minimum_revision=1",
            static body => !body.GetProperty("restricted_fields_redacted").GetBoolean());
        var reconciled = await WaitForAsync(owner, $"{root}/workforce-reconciliation-observations",
            static body => Items(body).All(item => Kind(item) != "access_only"));
        var graceMissing = Items(reconciled).Single(item => Kind(item) == "missing");
        var observationId = graceMissing.GetProperty("observation_id").GetString();
        var resolutionPath = $"{root}/workforce-observations/{observationId}/resolution";
        using var resolved = await owner.PutAsJsonAsync(resolutionPath,
            new { resolution = "dismissed", note = "Grace is tracked by the parent company." });
        using var replay = await owner.PutAsJsonAsync(resolutionPath,
            new { resolution = "dismissed", note = "Grace is tracked by the parent company." });
        using var changed = await owner.PutAsJsonAsync(resolutionPath,
            new { resolution = "resolved", note = "Different" });
        using var unknown = await owner.PutAsJsonAsync(
            $"{root}/workforce-observations/{Guid.NewGuid()}/resolution",
            new { resolution = "resolved", note = "Nothing" });
        var dismissed = await WaitForAsync(owner,
            $"{root}/workforce-reconciliation-observations?status=dismissed",
            static body => Items(body).Any());
        using var invalidKind = await owner.GetAsync(
            $"{root}/workforce-reconciliation-observations?kind=joiner");
        using var freeze = await owner.PostAsJsonAsync($"{root}/workforce-roster-snapshots", new { });
        using var outsiderList = await outsider.GetAsync(
            $"{root}/workforce-reconciliation-observations");
        using var outsiderResolve = await outsider.PutAsJsonAsync(resolutionPath,
            new { resolution = "resolved", note = "Intrusion" });
        using var outsiderCorrelate = await outsider.PutAsJsonAsync(
            $"{root}/people/{adaId}/membership-correlation",
            new { expected_revision = 2 });

        // Assert
        Assert.Equal(["access_only", "missing", "missing"],
            Items(initial).Select(Kind).Order().ToArray());
        Assert.Equal(HttpStatusCode.NoContent, correlated.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownMember.StatusCode);
        Assert.Equal(ownerUserId, linked.GetProperty("correlated_user_id").GetString());
        Assert.Equal(graceId, relationship.GetProperty("manager_person_id").GetString());
        Assert.Equal(graceId, graceMissing.GetProperty("person_ids")[0].GetString());
        Assert.Equal(HttpStatusCode.NoContent, resolved.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, replay.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        var closure = Assert.Single(Items(dismissed));
        Assert.Equal(observationId, closure.GetProperty("observation_id").GetString());
        Assert.Equal("member", closure.GetProperty("resolution").GetProperty("resolved_by")
            .GetProperty("kind").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, invalidKind.StatusCode);
        Assert.Equal(HttpStatusCode.OK, freeze.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderList.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderResolve.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderCorrelate.StatusCode);
        await using var mcp = await McpScenario.ConnectAsync(owner,
            new Uri(owner.BaseAddress!, "/mcp"));
        _ = await mcp.When("bdgrz.workforce.reconciliation.list", new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId,
            ["status"] = "dismissed",
        }).ExpectSuccess();
    }

    [Fact]
    public async Task ShouldReturnTransientLagAndRecoverResolutionGivenSplitWorkerRestart()
    {
        // Arrange
        var applicationName = $"compliance-reconcile-split-{Guid.NewGuid():N}";
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
            $"reconcile-split-{Guid.NewGuid():N}@example.com");
        var tenantId = await CreateTenantAsync(owner);
        var root = $"/api/v1/tenants/{tenantId}";
        var personId = await RecordWhenAuthorizedAsync(owner, $"{root}/people",
            new { display_name = "Ada Lovelace" });
        _ = await WaitForOkAsync(owner, $"{root}/people/{personId}?minimum_revision=1");
        var listed = await WaitForOkAsync(owner, $"{root}/workforce-reconciliation-observations");
        var observationId = Items(listed).Single(item => Kind(item) == "missing")
            .GetProperty("observation_id").GetString();
        await worker.StopAsync();

        // Act
        using var resolved = await owner.PutAsJsonAsync(
            $"{root}/workforce-observations/{observationId}/resolution",
            new { resolution = "resolved", note = "Work relationship to follow." });
        using var lagged = await owner.GetAsync($"{root}/workforce-reconciliation-observations");
        using var restarted = BuildWorker(applicationName);
        await restarted.StartAsync();
        try
        {
            var recovered = await WaitForAsync(owner,
                $"{root}/workforce-reconciliation-observations?status=resolved",
                static body => Items(body).Any());

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, resolved.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, lagged.StatusCode);
            Assert.Equal("true", lagged.Headers.GetValues("Portia-Transient").Single());
            Assert.Equal(observationId, Assert.Single(Items(recovered))
                .GetProperty("observation_id").GetString());
        }
        finally
        {
            await restarted.StopAsync();
        }
    }

    static JsonElement.ArrayEnumerator Items(JsonElement page) =>
        page.GetProperty("items").EnumerateArray();

    static string Kind(JsonElement item) => item.GetProperty("kind").GetString()!;

    static async Task<Guid> CreateTenantAsync(HttpClient owner)
    {
        using var tenant = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Reconciliation tenant",
            slug = $"recon-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, tenant.StatusCode);
        return Guid.Parse((await ReadAsync(tenant)).GetProperty("tenant_id").GetString()!);
    }

    /// <summary>Retries until the tenant bootstrap and workforce grant backfill have landed.</summary>
    static async Task<string> RecordWhenAuthorizedAsync(HttpClient owner, string path, object body)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(path, body);
            if (response.StatusCode == HttpStatusCode.OK)
                return (await ReadAsync(response)).GetProperty("person_id").GetString()!;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("Recording a person never became authorized after tenant bootstrap.");
    }

    static Task<JsonElement> WaitForOkAsync(HttpClient client, string path) =>
        WaitForAsync(client, path, static _ => true);

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
