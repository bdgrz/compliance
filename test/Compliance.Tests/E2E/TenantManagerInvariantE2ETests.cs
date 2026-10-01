using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     Broker proof for #432: two administrators suspending each other under a controlled
///     interleaving on the real broker leave exactly one active administrator, and the HTTP
///     controls (self-suspension, a suspended actor, reinstatement, uncontrolled concurrency)
///     keep the tenant manageable.
/// </summary>
[Trait("Category", "BrokerIntegration")]
public sealed class TenantManagerInvariantE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldKeepOneActiveAdministratorGivenConcurrentMutualSuspensionOnBroker()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker)
            .WithWebHostBuilder(host => host.ConfigureTestServices(services =>
                services.AddSingleton(new PlatformOperatorAuthority([Uuid.CreateVersion4()]))));
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var firstEmail = $"first-admin-{Guid.NewGuid():N}@example.com";
        var firstId = await TenantInvitationE2ETests.LoginAsync(firstClient, firstEmail);
        await TenantInvitationE2ETests.VerifyEmailAsync(factory, firstClient, firstId, firstEmail);
        using var registered = await firstClient.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Manager invariant",
            slug = $"managers-{Guid.NewGuid():N}"[..24],
            legal_name = "Manager Invariant LLC",
        });
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        var tenantId = Uuid.Parse((await registered.Content.ReadFromJsonAsync<Registration>())!.TenantId,
            CultureInfo.InvariantCulture);
        var administratorsTeamId = BuiltInRbac.AdministratorsTeamId(tenantId);
        await TenantInvitationE2ETests.WaitForMemberAccessReadyAsync(factory, firstClient, tenantId,
            firstId, administratorsTeamId, RbacPermissions.TenantRbacManage,
            $"/api/v1/tenants/{tenantId}/members/{firstId}");
        var secondEmail = $"second-admin-{Guid.NewGuid():N}@example.com";
        using (var invited = await firstClient.PostAsJsonAsync(
                   $"/api/v1/tenants/{tenantId}/member-invitations",
                   new { email_address = secondEmail, built_in_role = BuiltInRbac.TenantAdministrationRole }))
            Assert.Equal(HttpStatusCode.NoContent, invited.StatusCode);
        var token = await TenantInvitationE2ETests.WaitForInvitationDeliveryReadyAsync(factory, tenantId,
            secondEmail);
        var secondId = await TenantInvitationE2ETests.LoginAsync(secondClient, secondEmail);
        await TenantInvitationE2ETests.VerifyEmailAsync(factory, secondClient, secondId, secondEmail);
        using (var accepted = await secondClient.PostAsJsonAsync(
                   $"/api/v1/tenants/{tenantId}/invitations/acceptance",
                   new { email_address = secondEmail, token }))
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        await TenantInvitationE2ETests.WaitForMemberAccessReadyAsync(factory, secondClient, tenantId,
            secondId, administratorsTeamId, RbacPermissions.TenantRbacManage,
            $"/api/v1/tenants/{tenantId}/members/{secondId}");
        var first = Uuid.Parse(firstId, CultureInfo.InvariantCulture);
        var second = Uuid.Parse(secondId, CultureInfo.InvariantCulture);

        // Act
        using var selfSuspension = await firstClient.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/members/{firstId}/suspensions", new { reason = "Self." });
        var interleaved = await SuspendConcurrentlyAsync(factory, tenantId, first, second);
        var firstSuspended = await IsSuspendedAsync(factory, tenantId, first);
        var secondSuspended = await IsSuspendedAsync(factory, tenantId, second);
        var (survivorClient, survivorId, lockedId) = firstSuspended
            ? (secondClient, secondId, firstId)
            : (firstClient, firstId, secondId);
        using var reinstated = await survivorClient.DeleteAsync(
            $"/api/v1/tenants/{tenantId}/members/{lockedId}/suspensions");
        var reinstatedSource = !await IsSuspendedAsync(factory, tenantId,
            Uuid.Parse(lockedId, CultureInfo.InvariantCulture));
        await TenantInvitationE2ETests.WaitForMemberAccessReadyAsync(factory,
            survivorClient == firstClient ? secondClient : firstClient, tenantId, lockedId,
            administratorsTeamId, RbacPermissions.TenantRbacManage,
            $"/api/v1/tenants/{tenantId}/members/{lockedId}");
        var racing = await Task.WhenAll(
            firstClient.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/members/{secondId}/suspensions",
                new { reason = "Uncontrolled race." }),
            secondClient.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/members/{firstId}/suspensions",
                new { reason = "Uncontrolled race." }));
        var activeAfterRace = !await IsSuspendedAsync(factory, tenantId, first) ||
                              !await IsSuspendedAsync(factory, tenantId, second);
        foreach (var response in racing)
            response.Dispose();

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, selfSuspension.StatusCode);
        Assert.Single(interleaved, result => result.IsSuccess);
        var denied = Assert.Single(interleaved, result => !result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(denied.Error).Kind);
        Assert.True(firstSuspended ^ secondSuspended, "Exactly one administrator must stay active.");
        Assert.NotEqual(survivorId, lockedId);
        Assert.Equal(HttpStatusCode.NoContent, reinstated.StatusCode);
        Assert.True(reinstatedSource);
        Assert.True(activeAfterRace, "An uncontrolled race must still leave an active administrator.");
        Assert.All(racing, response => Assert.True(
            response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.Conflict or HttpStatusCode.NotFound,
            response.StatusCode.ToString()));
    }

    /// <summary>
    ///     Runs both suspensions against the broker with their guard reads held until both have
    ///     read, so each decision starts from the same guard state.
    /// </summary>
    static async Task<Result[]> SuspendConcurrentlyAsync(WebApplicationFactory<Program> factory,
        Uuid tenantId, Uuid first, Uuid second)
    {
        var gate = new GuardReadGate(2);
        async Task<Result> SuspendAsync(Uuid actor, Uuid target)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var reader = new GatedReader(services.GetRequiredService<IAggregateReader>(), gate);
            var executor = new AggregateExecutor(reader, services.GetRequiredService<IAggregateWriter>());
            var managers = new TenantManagerInvariant(reader, executor,
                services.GetRequiredService<ITeamMemberDirectoryReader>());
            var handler = new SuspendMemberHandler(executor, TimeProvider.System, managers, reader);
            return await handler.HandleAsync(new RequestContext<SuspendMember>(
                new SuspendMember(tenantId, target, "Controlled mutual suspension."),
                new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("iss", "bdgrz"), new Claim("sub", actor.ToString())], "BdgrzSession"))),
                CancellationToken.None);
        }
        return await Task.WhenAll(SuspendAsync(first, second), SuspendAsync(second, first));
    }

    static async Task<bool> IsSuspendedAsync(WebApplicationFactory<Program> factory, Uuid tenantId,
        Uuid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new Member(tenantId, userId))).IsSuspended;
    }

    sealed class GuardReadGate(int count)
    {
        readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int _remaining = count;

        public async Task PassAsync(CancellationToken ct)
        {
            if (Volatile.Read(ref _remaining) <= 0)
                return;
            if (Interlocked.Decrement(ref _remaining) == 0)
                _released.TrySetResult();
            await _released.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        }
    }

    sealed class GatedReader(IAggregateReader inner, GuardReadGate gate) : IAggregateReader
    {
        public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            var hydrated = await inner.HydrateAsync(aggregate, ct);
            if (aggregate is TenantManagerGuard)
                await gate.PassAsync(ct);
            return hydrated;
        }
    }

    sealed record Registration([property: JsonPropertyName("tenant_id")] string TenantId);
}
