using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

/// <summary>
///     Runs <see cref="RegisterTenant" /> through Portia's real composed lifecycle — this is what a
///     unit test against <see cref="PlatformOperatorAuthorizer" /> or
///     <see cref="RegisterTenantSlugAvailabilityGuard" /> in isolation cannot show: that they are
///     actually wired to <see cref="RegisterTenant" />, and run in the right order.
/// </summary>
public sealed class RegisterTenantRequestScenarioTests
{
    [Fact]
    public async Task ShouldAuthorizeGuardAndHandleGivenAnAvailableSlug()
    {
        await using var provider = BuildProvider();

        await RequestScenario.For(provider)
            .GivenActor(BdgrzActor())
            .When(new RegisterTenant("Acme", "acme"))
            .ExpectAuthorized()
            .ExpectGuardPassed<RegisterTenantSlugAvailabilityGuard>()
            .ExpectHandled()
            .ExpectSuccess();
    }

    [Fact]
    public async Task ShouldDenyGivenAnActorWithoutABdgrzIdentity()
    {
        await using var provider = BuildProvider();

        await RequestScenario.For(provider)
            .GivenActor(RequestActor.Anonymous)
            .When(new RegisterTenant("Acme", "acme"))
            .ExpectDenied(RequestErrorKind.Unauthorized)
            .ExpectNotHandled();
    }

    [Fact]
    public async Task ShouldDenyAPlatformUserWithoutAnOperatorGrant()
    {
        await using var provider = BuildProvider(developerAuthentication: false);

        await RequestScenario.For(provider)
            .GivenActor(BdgrzActor())
            .When(new RegisterTenant("Acme", "acme"))
            .ExpectDenied(RequestErrorKind.Forbidden)
            .ExpectNotHandled();
    }

    [Fact]
    public async Task ShouldShortCircuitAtTheGuardGivenAnOccupiedSlug()
    {
        await using var provider = BuildProvider();
        await using (var seed = provider.CreateAsyncScope())
        {
            var reader = seed.ServiceProvider.GetRequiredService<IAggregateReader>();
            var writer = seed.ServiceProvider.GetRequiredService<IAggregateWriter>();
            var occupied = await reader.HydrateAsync(new TenantSlug("acme"), CancellationToken.None);
            _ = occupied.Register(Uuid.CreateVersion4());
            await writer.SaveAsync(occupied, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        }

        await RequestScenario.For(provider)
            .GivenActor(BdgrzActor())
            .When(new RegisterTenant("Acme", "acme"))
            .ExpectAuthorized()
            .ExpectGuardFailed<RegisterTenantSlugAvailabilityGuard>(RequestErrorKind.Conflict)
            .ExpectNotHandled();
    }

    static ServiceProvider BuildProvider(bool developerAuthentication = true)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton(new PlatformOperatorAuthority([], developerAuthentication));
        services.AddPortia()
            .AddRequestHandler<RegisterTenantHandler>()
            .AddRequestAuthorizer<PlatformOperatorAuthorizer>()
            .AddRequestGuard<RegisterTenantSlugAvailabilityGuard>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    static ClaimsPrincipal BdgrzActor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));
}
