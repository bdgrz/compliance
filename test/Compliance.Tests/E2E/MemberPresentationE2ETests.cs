using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Fitz;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class MemberPresentationE2ETests(BrokerStackFixture broker) : IClassFixture<BrokerStackFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRetainAuthorizedMemberPresentationGivenSourceChangesAndHostReplay(bool splitHosts)
    {
        // Arrange
        var applicationName = $"compliance-member-presentation-{Guid.NewGuid():N}";
        IHost? worker = null;
        if (splitHosts)
        {
            worker = BuildWorker(applicationName);
            await worker.StartAsync();
        }
        try
        {
            Uuid operatorId;
            await using (var seedFactory = E2EAppFactory.Create(broker, applicationName))
            {
                using var operatorClient = seedFactory.CreateClient();
                operatorId = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(operatorClient,
                    "member-presentation-operator@example.com"), CultureInfo.InvariantCulture);
            }
            await using var factory = E2EAppFactory.Create(broker, applicationName)
                .WithWebHostBuilder(host => host.ConfigureTestServices(services =>
                    services.AddSingleton(new PlatformOperatorAuthority([operatorId]))));
            var previousMode = TestHostMode.Current;
            HttpClient owner;
            HttpClient outsider;
            try
            {
                TestHostMode.Set(splitHosts ? "api" : "standalone");
                owner = factory.CreateClient();
                outsider = factory.CreateClient();
            }
            finally
            {
                TestHostMode.Set(previousMode);
            }
            using (owner)
            using (outsider)
            {
                var email = $"member-presentation-{Guid.NewGuid():N}@example.com";
                var userIdText = await TenantInvitationE2ETests.LoginAsync(owner, email);
                var userId = Uuid.Parse(userIdText, CultureInfo.InvariantCulture);
                await TenantInvitationE2ETests.LoginAsync(outsider, $"member-outsider-{Guid.NewGuid():N}@example.com");
                await TenantInvitationE2ETests.VerifyEmailAsync(factory, owner, userIdText, email,
                    worker?.Services.GetRequiredService<MockEmailChallengeDelivery>());
                var tenantId = await CreateTenantAsync(owner);
                var root = $"/api/v1/tenants/{tenantId}";
                var paths = new[]
                {
                    $"{root}/members/{userId}", $"{root}/members?limit=1",
                    $"{root}/teams/{BuiltInRbac.AdministratorsTeamId(tenantId)}/members?limit=1",
                };
                await AccessGrantE2ESupport.IssueFounderOrganizationGrantAsync(owner, tenantId);

                // Act
                await ObserveProfileAsync(factory.Services, userId, "Provider member");
                await AssertReadsAsync(owner, paths, userId, "Provider member", email);
                using var created = await owner.PostAsJsonAsync($"{root}/people", new
                {
                    display_name = "Roster member",
                    work_email = "unverified-roster@example.test",
                });
                Assert.Equal(HttpStatusCode.OK, created.StatusCode);
                var personId = (await ReadAsync(created)).GetProperty("person_id").GetString();
                var personPath = $"{root}/people/{personId}";
                using var correlated = await owner.PutAsJsonAsync($"{personPath}/membership-correlation",
                    new { expected_revision = 1, user_id = userId });
                Assert.Equal(HttpStatusCode.NoContent, correlated.StatusCode);
                await AssertReadsAsync(owner, paths, userId, "Roster member", email);
                var otherTenant = await CreateTenantAsync(owner);
                await AccessGrantE2ESupport.IssueFounderOrganizationGrantAsync(owner, otherTenant);
                await AssertReadsAsync(owner,
                    [$"/api/v1/tenants/{otherTenant}/members/{userId}"], userId, "Provider member", email);
                using var alienPerson = await owner.GetAsync($"/api/v1/tenants/{otherTenant}/people/{personId}");
                Assert.Equal(HttpStatusCode.NotFound, alienPerson.StatusCode);
                using var revised = await owner.PutAsJsonAsync(personPath,
                    new { expected_revision = 2, display_name = "Roster renamed" });
                Assert.Equal(HttpStatusCode.NoContent, revised.StatusCode);
                await AssertReadsAsync(owner, paths, userId, "Roster renamed", email);
                await ObserveProfileAsync(factory.Services, userId, "Provider renamed");
                await AssertReadsAsync(owner, paths, userId, "Roster renamed", email);
                using var unlinked = await owner.PutAsJsonAsync($"{personPath}/membership-correlation",
                    new { expected_revision = 3, user_id = (string?)null });
                Assert.Equal(HttpStatusCode.NoContent, unlinked.StatusCode);
                await AssertReadsAsync(owner, paths, userId, "Provider renamed", email);
                await ObserveProfileAsync(factory.Services, userId, null);
                await AssertReadsAsync(owner, paths, userId, email, email);
                await using var mcp = await McpScenario.ConnectAsync(owner, new Uri(owner.BaseAddress!, "/mcp"));
                var mcpMember = await mcp.When("bdgrz.tenant.member.get", new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantId.ToString(),
                    ["user_id"] = userId.ToString(),
                }).ExpectSuccess();
                var mcpBody = Assert.IsType<JsonElement>(mcpMember.StructuredJson).GetProperty("result");
                Assert.Equal(email, mcpBody.GetProperty("display_name").GetString());
                Assert.Equal(email, mcpBody.GetProperty("verified_email_address").GetString());

                // Assert
                foreach (var path in paths)
                {
                    using var denied = await outsider.GetAsync(path);
                    Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
                    Assert.DoesNotContain(email, await denied.Content.ReadAsStringAsync(), StringComparison.Ordinal);
                }
                await AssertReadsAsync(owner,
                    [$"/api/v1/tenants/{otherTenant}/members/{userId}"], userId, email, email);

                if (splitHosts)
                {
                    // Clear only the two derived presentation resources in this class's isolated
                    // broker. Keep all source, membership, email and authority data unchanged.
                    await worker!.StopAsync();
                    worker.Dispose();
                    worker = null;
                    await using (var scope = factory.Services.CreateAsyncScope())
                    {
                        var client = scope.ServiceProvider.GetRequiredService<IKvClient>();
                        var capture = new RouteCapture(client);
                        _ = await new FitzPersonDirectory(capture).ReadAsync(tenantId, userId);
                        await ClearProjectionAsync(client, capture.LastRoute!, "kv://bdgrz/person-directory-v2/projection-");
                        _ = await new FitzPlatformUserDirectory(capture).ReadAsync(userId);
                        await ClearProjectionAsync(client, capture.LastRoute!, "kv://bdgrz/platform-user-directory-v2/projection-");
                    }
                    await ObserveProfileAsync(factory.Services, userId, "After worker restart");
                    worker = BuildWorker(applicationName);
                    await worker.StartAsync();
                    await AssertReadsAsync(owner, paths, userId, "After worker restart", email);
                    using var replayedPerson = await owner.GetAsync($"{personPath}?minimum_revision=4");
                    Assert.Equal(HttpStatusCode.OK, replayedPerson.StatusCode);
                    Assert.Equal("Roster renamed", (await ReadAsync(replayedPerson)).GetProperty("display_name").GetString());
                }
            }
        }
        finally
        {
            if (worker is not null)
            {
                await worker.StopAsync();
                worker.Dispose();
            }
        }
    }

    static async Task ObserveProfileAsync(IServiceProvider services, Uuid userId, string? name)
    {
        // Synthetic authenticated provider/session proofs exercise the actual identity handlers
        // and persisted broker source; they do not claim an external provider's operating evidence.
        await using var scope = services.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var provider = new ClaimsIdentity(
            [new("iss", "https://member-provider.example.test"), new("sub", userId.ToString()),
                new("email", $"unverified-provider-{userId}@example.test")], "oidc");
        if (name is not null)
            provider.AddClaim(new Claim("name", name));
        var session = new ClaimsIdentity([new("iss", "bdgrz"), new("sub", userId.ToString())], "BdgrzSession");
        var linked = await new LinkOidcProviderIdentityHandler(executor).HandleAsync(
            new RequestContext<LinkOidcProviderIdentity>(new LinkOidcProviderIdentity(),
                new ClaimsPrincipal([provider, session])), CancellationToken.None);
        Assert.True(linked.IsSuccess);
        Assert.Equal(userId, linked.Value.UserId);
        var continued = await new ContinueWithOidcProviderHandler(new UserIdentityContinuation(executor)).HandleAsync(
            new RequestContext<ContinueWithOidcProvider>(new ContinueWithOidcProvider(),
                new ClaimsPrincipal(provider)), CancellationToken.None);
        Assert.True(continued.IsSuccess);
        Assert.Equal(userId, continued.Value.UserId);
    }

    static async Task AssertReadsAsync(HttpClient client, string[] paths, Uuid userId,
        string name, string email)
    {
        foreach (var path in paths)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
            JsonElement member = default;
            while (DateTimeOffset.UtcNow < deadline)
            {
                using var response = await client.GetAsync(path);
                Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict,
                    $"{path}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var body = await ReadAsync(response);
                    var items = body.TryGetProperty("items", out var page) ? page.EnumerateArray().ToArray() : [body];
                    member = items.SingleOrDefault(item => item.TryGetProperty("user_id", out var id) &&
                        id.GetString() == userId.ToString());
                    if (member.ValueKind == JsonValueKind.Object &&
                        member.TryGetProperty("display_name", out var display) && display.GetString() == name)
                        break;
                }
                await Task.Delay(250);
            }
            Assert.Equal(JsonValueKind.Object, member.ValueKind);
            Assert.Equal(name, member.GetProperty("display_name").GetString());
            Assert.Equal(email, member.GetProperty("verified_email_address").GetString());
            Assert.False(member.TryGetProperty("personal_contact", out _));
        }
    }

    static async Task<Uuid> CreateTenantAsync(HttpClient client)
    {
        using var created = await client.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Member presentation tenant",
            legal_name = "Member presentation legal entity",
            slug = $"member-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        return Uuid.Parse((await ReadAsync(created)).GetProperty("tenant_id").GetString()!, CultureInfo.InvariantCulture);
    }

    static async Task ClearProjectionAsync(IKvClient client, string route, string prefix)
    {
        Assert.StartsWith(prefix, route, StringComparison.Ordinal);
        await using var tx = await client.BeginAsync(route, KvDurability.Sync);
        await tx.DeleteRangeAsync(ReadOnlyMemory<byte>.Empty, new byte[] { byte.MaxValue });
        await tx.CommitAsync();
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

    sealed class RouteCapture(IKvClient inner) : IKvClient
    {
        public string? LastRoute { get; private set; }

        public Task<IKvTransaction> BeginAsync(string route, KvDurability durability,
            KvMode mode = KvMode.ReadWrite, CancellationToken ct = default)
        {
            LastRoute = route;
            return inner.BeginAsync(route, durability, mode, ct);
        }

        public Task<KvSubscription> SubscribeAsync(string pattern, CancellationToken ct = default) =>
            inner.SubscribeAsync(pattern, ct);
    }
}
