using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Providers;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderPagingHttpMcpTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldExhaustScopedCurrentAndHistoryPagesGivenTwoTenantsAndHttpOrMcp(bool mcp)
    {
        // Arrange
        await using var factory = ProviderHttpMcpTests.CreateFactory();
        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/developer-user-sessions", new { email_address = "provider-pages@example.com" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Uuid[] tenants = [Uuid.CreateVersion4(), Uuid.CreateVersion4()];
        var identities = new Dictionary<Uuid, List<Uuid>>();
        foreach (var tenant in tenants)
        {
            identities[tenant] = [];
            for (var index = 0; index < 2; index++)
            {
                using var created = await client.PostAsync(Path(tenant), Body($"Tenant {tenant} provider {index}"));
                Assert.Equal(HttpStatusCode.OK, created.StatusCode);
                var registration = JsonSerializer.Deserialize(await created.Content.ReadAsStringAsync(), ComplianceCoreJsonContext.Default.ProviderRegistration)!;
                identities[tenant].Add(registration.ProviderId);
                using var revised = await client.PutAsync(Path(tenant, registration.ProviderId), Body($"Revised {tenant} provider {index}", true));
                Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
            }
            await CatchUpAsync(factory.Services, tenant);
        }
        await using var scenario = mcp ? await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp")) : null;

        // Act
        foreach (var tenant in tenants)
        {
            var otherTenant = tenants.Single(item => item != tenant);
            var first = await ReadPageAsync(client, scenario, tenant, null, null);
            var next = first.GetProperty("next_cursor").GetString();
            Assert.NotNull(next);
            var second = await ReadPageAsync(client, scenario, tenant, null, next);
            Assert.Equal(JsonValueKind.Null, second.GetProperty("next_cursor").ValueKind);
            var current = first.GetProperty("items").EnumerateArray().Concat(second.GetProperty("items").EnumerateArray()).ToArray();
            Assert.Equal(2, current.Length);
            Assert.Equal(identities[tenant].Order(), current.Select(item => Uuid.Parse(item.GetProperty("provider_id").GetString()!, null)).Order());
            Assert.All(current, item =>
            {
                Assert.Equal(2, item.GetProperty("revision").GetInt64());
                Assert.Equal(tenant.ToString(), item.GetProperty("tenant_id").GetString());
                Assert.StartsWith($"Revised {tenant} provider ", item.GetProperty("content").GetProperty("name").GetString());
            });
            await ExpectCursorDeniedAsync(client, scenario, otherTenant, null, next);
            foreach (var provider in identities[tenant])
            {
                var firstHistory = await ReadPageAsync(client, scenario, tenant, provider, null);
                var historyCursor = firstHistory.GetProperty("next_cursor").GetString();
                Assert.NotNull(historyCursor);
                var secondHistory = await ReadPageAsync(client, scenario, tenant, provider, historyCursor);
                var history = firstHistory.GetProperty("items").EnumerateArray().Concat(secondHistory.GetProperty("items").EnumerateArray()).OrderBy(item => item.GetProperty("revision").GetInt64()).ToArray();
                Assert.Equal(JsonValueKind.Null, secondHistory.GetProperty("next_cursor").ValueKind);
                Assert.Equal(new long[] { 1, 2 }, history.Select(item => item.GetProperty("revision").GetInt64()));
                Assert.StartsWith($"Tenant {tenant} provider ", history[0].GetProperty("content").GetProperty("name").GetString());
                Assert.StartsWith($"Revised {tenant} provider ", history[1].GetProperty("content").GetProperty("name").GetString());
                Assert.All(history, item => Assert.Equal(provider.ToString(), item.GetProperty("provider_id").GetString()));
                var otherProvider = identities[tenant].Single(item => item != provider);
                await ExpectCursorDeniedAsync(client, scenario, tenant, otherProvider, historyCursor);
                await ExpectCursorDeniedAsync(client, scenario, tenant, provider, next);
            }
        }

        // Assert
        Assert.Equal(4, identities.Values.Sum(items => items.Count));
    }

    static string Path(Uuid tenant, Uuid? provider = null) => $"/api/v1/tenants/{tenant}/providers" + (provider is null ? "" : $"/{provider}");

    static StringContent Body(string name, bool revise = false) => new(
        (revise ? "{\"expected_revision\":1,\"content\":" : "{\"content\":") +
        JsonSerializer.Serialize(new ProviderContent(name, "supplier"), ComplianceCoreJsonContext.Default.ProviderContent) + "}", Encoding.UTF8, "application/json");

    static Dictionary<string, object?> Arguments(Uuid tenant, Uuid? provider, string? cursor)
    {
        var arguments = new Dictionary<string, object?> { ["tenant_id"] = tenant.ToString(), ["limit"] = 1 };
        if (provider is not null)
            arguments["provider_id"] = provider.ToString();
        if (cursor is not null)
            arguments["cursor"] = cursor;
        return arguments;
    }

    static string ListPath(Uuid tenant, Uuid? provider, string? cursor) => Path(tenant, provider) +
        (provider is null ? "" : "/revisions") + "?limit=1" + (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor));

    static async Task<JsonElement> ReadPageAsync(HttpClient client, McpScenario? scenario, Uuid tenant, Uuid? provider, string? cursor)
    {
        if (scenario is not null)
        {
            var snapshot = await scenario.When(provider is null ? "bdgrz.providers.list" : "bdgrz.provider.revisions.list", Arguments(tenant, provider, cursor)).ExpectSuccess();
            return Assert.IsType<JsonElement>(snapshot.StructuredJson).GetProperty("result");
        }
        using var response = await client.GetAsync(ListPath(tenant, provider, cursor));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    static async Task ExpectCursorDeniedAsync(HttpClient client, McpScenario? scenario, Uuid tenant, Uuid? provider, string cursor)
    {
        if (scenario is not null)
        {
            await scenario.When(provider is null ? "bdgrz.providers.list" : "bdgrz.provider.revisions.list", Arguments(tenant, provider, cursor)).ExpectFailure("Validation");
            return;
        }
        using var response = await client.GetAsync(ListPath(tenant, provider, cursor));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    static async Task CatchUpAsync(IServiceProvider provider, Uuid tenant)
    {
        using var scope = provider.CreateScope();
        var directory = scope.ServiceProvider.GetRequiredService<FitzProviderDirectory>();
        var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var checkpoint = await directory.LoadCheckpointAsync(tenant);
        var pattern = EventStreamPattern.ForPattern(tenant.ToString(), ProviderRegister.Area);
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(FitzProviderDirectory.ProjectorName, pattern), checkpoint));
        var cursor = checkpoint.Cursor;
        await foreach (var record in events.ReadAsync(pattern, checkpoint.Cursor, CancellationToken.None))
        {
            await directory.ApplyAsync(record.Event);
            cursor = record.NextCursor;
        }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }
}
