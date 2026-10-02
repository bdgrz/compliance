using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
/// Proves access-owner separation of duties, legacy-instance scope decisions, and the scope
/// projection through the real broker, standalone and split-host (#463).
/// </summary>
[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class AccessReviewScopeOwnerE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    static readonly DateTimeOffset LegacyDeclaredAt = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldEnforceAccessOwnerAndProjectLegacyScopeGivenCorrelatedOwner(
        bool splitHosts)
    {
        // Arrange
        var applicationName = $"compliance-scope-owner-{Guid.NewGuid():N}";
        using var worker = splitHosts ? BuildWorker(applicationName) : null;
        if (worker is not null)
            await worker.StartAsync();
        try
        {
            await using var factory = E2EAppFactory.Create(broker, applicationName);
            using var owner = CreateClient(factory, splitHosts);
            using var outsider = CreateClient(factory, splitHosts);
            var scenario = await ArrangeAsync(factory, owner, outsider);
            var effective = DateTimeOffset.UtcNow.AddDays(-30);

            // Act
            // The owner is not yet linked to the person, so the first decision is unconflicted.
            using var first = await DecideWhenAuthorizedAsync(owner, scenario, 0, "included",
                effective);
            using var correlated = await owner.PutAsJsonAsync(
                $"{scenario.Root}/people/{scenario.PersonId}/membership-correlation",
                new { expected_revision = 1, user_id = scenario.OwnerUserId });
            using var conflicted = await DecideAsync(owner, scenario, 1, "excluded",
                effective.AddDays(1));
            using var cleared = await owner.PutAsJsonAsync(
                $"{scenario.Root}/people/{scenario.PersonId}/membership-correlation",
                new { expected_revision = 2 });
            using var unlinked = await DecideAsync(owner, scenario, 1, "excluded",
                effective.AddDays(1));
            var projected = await WaitForAsync(owner, scenario.ScopesPath,
                static body => Items(body).Any(item =>
                    item.GetProperty("status").GetString() == "excluded"));
            var history = await WaitForAsync(owner, scenario.ScopePath, static _ => true);
            using var outsiderList = await outsider.GetAsync(scenario.ScopesPath);
            using var outsiderDecide = await DecideAsync(outsider, scenario, 2, "included",
                effective.AddDays(2));
            using var otherTenantList = await outsider.GetAsync(
                $"/api/v1/tenants/{scenario.OtherTenantId}/applications/{scenario.ApplicationId}/access-review-scopes");
            await using var mcp = await McpScenario.ConnectAsync(owner,
                new Uri(owner.BaseAddress!, "/mcp"));
            var mcpList = await mcp.When("bdgrz.application.access_review_scopes.list",
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = scenario.TenantId,
                    ["application_id"] = scenario.ApplicationId,
                }).ExpectSuccess();

            // Assert
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, correlated.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, conflicted.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, cleared.StatusCode);
            Assert.Equal(HttpStatusCode.OK, unlinked.StatusCode);
            var instance = Assert.Single(Items(projected));
            Assert.Equal(scenario.LegacyId.ToString(),
                instance.GetProperty("system_instance_id").GetString());
            Assert.Equal("excluded", instance.GetProperty("status").GetString());
            Assert.Equal(2, instance.GetProperty("decision_count").GetInt64());
            Assert.Equal(2, history.GetProperty("decisions").GetArrayLength());
            Assert.Equal(HttpStatusCode.NotFound, outsiderList.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, outsiderDecide.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, otherTenantList.StatusCode);
            var mcpItems = Assert.IsType<JsonElement>(mcpList.StructuredJson)
                .GetProperty("result");
            Assert.Equal(scenario.LegacyId.ToString(), Assert.Single(Items(mcpItems))
                .GetProperty("system_instance_id").GetString());
        }
        finally
        {
            if (worker is not null)
                await worker.StopAsync();
        }
    }

    [Fact]
    public async Task ShouldReturnTransientLagAndRecoverProjectionGivenSplitWorkerRestart()
    {
        // Arrange
        var applicationName = $"compliance-scope-lag-{Guid.NewGuid():N}";
        using var worker = BuildWorker(applicationName);
        await worker.StartAsync();
        await using var factory = E2EAppFactory.Create(broker, applicationName);
        using var owner = CreateClient(factory, true);
        using var outsider = CreateClient(factory, true);
        var scenario = await ArrangeAsync(factory, owner, outsider);
        _ = await WaitForAsync(owner, scenario.ScopesPath, static body => Items(body).Any());
        using var first = await DecideWhenAuthorizedAsync(owner, scenario, 0, "included",
            DateTimeOffset.UtcNow.AddDays(-30));
        await worker.StopAsync();

        // Act
        using var second = await DecideAsync(owner, scenario, 1, "excluded",
            DateTimeOffset.UtcNow.AddDays(-20));
        using var lagged = await owner.GetAsync(scenario.ScopesPath);
        using var restarted = BuildWorker(applicationName);
        await restarted.StartAsync();
        try
        {
            var recovered = await WaitForAsync(owner, scenario.ScopesPath,
                static body => Items(body).Any(item =>
                    item.GetProperty("decision_count").GetInt64() == 2));

            // Assert
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, lagged.StatusCode);
            Assert.Equal("true", lagged.Headers.GetValues("Portia-Transient").Single());
            Assert.Equal("excluded", Assert.Single(Items(recovered))
                .GetProperty("status").GetString());
        }
        finally
        {
            await restarted.StopAsync();
        }
    }

    sealed record Scenario(Guid TenantId, Guid OtherTenantId, Guid ApplicationId, Uuid LegacyId,
        string PersonId, string OwnerUserId)
    {
        public string Root => $"/api/v1/tenants/{TenantId}";
        public string ApplicationPath => $"{Root}/applications/{ApplicationId}";
        public string ScopesPath => $"{ApplicationPath}/access-review-scopes";
        public string ScopePath =>
            $"{ApplicationPath}/system-instances/{LegacyId}/access-review-scope";
    }

    static async Task<Scenario> ArrangeAsync(WebApplicationFactory<Program> factory,
        HttpClient owner, HttpClient outsider)
    {
        await TenantInvitationE2ETests.LoginAsync(owner,
            $"scope-owner-{Guid.NewGuid():N}@example.com");
        await TenantInvitationE2ETests.LoginAsync(outsider,
            $"scope-outsider-{Guid.NewGuid():N}@example.com");
        var tenantId = await CreateTenantAsync(owner, "Scope owner tenant");
        var otherTenantId = await CreateTenantAsync(outsider, "Other scope tenant");
        await AccessGrantE2ESupport.IssueFounderOrganizationGrantAsync(owner, tenantId);
        var root = $"/api/v1/tenants/{tenantId}";
        var personId = await RecordWhenAuthorizedAsync(owner, $"{root}/people",
            new { display_name = "Ada Lovelace" }, "person_id");
        _ = await WaitForAsync(owner, $"{root}/people/{personId}?minimum_revision=1",
            static _ => true);
        var reconciliation = await WaitForAsync(owner,
            $"{root}/workforce-reconciliation-observations",
            static body => Items(body).Any(item =>
                item.GetProperty("kind").GetString() == "access_only"));
        var ownerUserId = Items(reconciliation)
            .First(item => item.GetProperty("kind").GetString() == "access_only")
            .GetProperty("user_ids")[0].GetString()!;
        var applicationId = Guid.Parse(await RecordWhenAuthorizedAsync(owner,
            $"{root}/applications", new
            {
                name = "Payroll",
                purpose = "Run payroll",
                access_owner_person_id = personId,
            }, "application_id"), CultureInfo.InvariantCulture);
        _ = await WaitForAsync(owner,
            $"{root}/applications/{applicationId}?minimum_revision=1", static _ => true);

        // The pre-cutover binary appended instance declarations to the application stream, so
        // the registrant is a different member from the access owner.
        var legacyId = Uuid.CreateVersion4();
        var tenantUuid = Uuid.Parse(tenantId.ToString(), CultureInfo.InvariantCulture);
        var applicationUuid = Uuid.Parse(applicationId.ToString(), CultureInfo.InvariantCulture);
        var store = factory.Services.GetRequiredService<IEventStore>();
        await store.AppendAsync(new EventStreamAddress(tenantId.ToString(), "applications",
            applicationId.ToString()), 1,
        [
            DomainEventSeed.Attach(new SystemInstanceDeclared(tenantUuid, applicationUuid,
                legacyId, 2, "Legacy production", "production", null, "legacy-prod",
                Uuid.CreateVersion4(), "Legacy writer", LegacyDeclaredAt), applicationUuid, 2),
        ]);
        _ = await WaitForAsync(owner,
            $"{root}/applications/{applicationId}/system-instances/{legacyId}?minimum_application_revision=2&minimum_instance_revision=1",
            static _ => true);
        return new Scenario(tenantId, otherTenantId, applicationId, legacyId, personId,
            ownerUserId);
    }

    static Task<HttpResponseMessage> DecideAsync(HttpClient client, Scenario scenario,
        long decisionCount, string decision, DateTimeOffset effectiveFrom) =>
        client.PostAsJsonAsync(
            $"{scenario.ApplicationPath}/system-instances/{scenario.LegacyId}/access-review-scope-decisions",
            new
            {
                expected_system_instance_revision = 1,
                expected_decision_count = decisionCount,
                decision,
                reason = "Holds production payroll data.",
                effective_from = effectiveFrom,
            });

    /// <summary>Retries until tenant bootstrap grants have landed.</summary>
    static async Task<HttpResponseMessage> DecideWhenAuthorizedAsync(HttpClient client,
        Scenario scenario, long decisionCount, string decision, DateTimeOffset effectiveFrom)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (true)
        {
            var response = await DecideAsync(client, scenario, decisionCount, decision,
                effectiveFrom);
            if (response.StatusCode == HttpStatusCode.OK || DateTimeOffset.UtcNow >= deadline)
                return response;
            response.Dispose();
            await Task.Delay(250);
        }
    }

    static JsonElement.ArrayEnumerator Items(JsonElement page) =>
        page.GetProperty("items").EnumerateArray();

    static HttpClient CreateClient(WebApplicationFactory<Program> factory, bool apiOnly)
    {
        if (!apiOnly)
            return factory.CreateClient();
        var previousMode = TestHostMode.Current;
        try
        {
            TestHostMode.Set("api");
            return factory.CreateClient();
        }
        finally
        {
            TestHostMode.Set(previousMode);
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

    static async Task<Guid> CreateTenantAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/tenants", new
        {
            name,
            slug = $"scope-{Guid.NewGuid():N}"[..24],
        });
        return Guid.Parse((await ReadAsync(response)).GetProperty("tenant_id").GetString()!,
            CultureInfo.InvariantCulture);
    }

    static async Task<string> RecordWhenAuthorizedAsync(HttpClient client, string path,
        object body, string idProperty)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.PostAsJsonAsync(path, body);
            if (response.StatusCode == HttpStatusCode.OK)
                return (await ReadAsync(response)).GetProperty(idProperty).GetString()!;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException($"{path} stayed unauthorized after tenant bootstrap.");
    }

    /// <summary>Retries transient projection lag until the body satisfies the condition.</summary>
    static async Task<JsonElement> WaitForAsync(HttpClient client, string path,
        Func<JsonElement, bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        var last = string.Empty;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            last = $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}";
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
        throw new TimeoutException($"{path} did not reach the expected state: {last}");
    }

    static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"{(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }
}
