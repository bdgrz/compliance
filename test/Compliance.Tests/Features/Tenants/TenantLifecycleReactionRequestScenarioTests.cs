using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantLifecycleReactionRequestScenarioTests
{
    [Fact]
    public async Task ShouldDenyRequestGivenNonSystemActor()
    {
        // Arrange
        await using var provider = BuildProvider();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));

        await RequestScenario.For(provider)
            .GivenActor(actor)
            // Act
            .When(new RegisterTenantSlug(Uuid.CreateVersion4(), "acme"))
            // Assert
            .ExpectDenied(RequestErrorKind.Forbidden)
            .ExpectNotHandled();
    }

    [Fact]
    public async Task ShouldHandleRequestGivenSystemActor()
    {
        // Arrange
        await using var provider = BuildProvider();

        await RequestScenario.For(provider)
            .GivenActor(RequestActor.System)
            // Act
            .When(new RegisterTenantSlug(Uuid.CreateVersion4(), "acme"))
            // Assert
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectSuccess();
    }

    static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia()
            .AddRequestHandler<RegisterTenantSlugHandler>()
            .AddRequestAuthorizer<TenantLifecycleReactionAuthorizer>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
