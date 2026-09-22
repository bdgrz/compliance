using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class ContinueWithOidcProviderRequestScenarioTests
{
    [Fact]
    public async Task ShouldDenyRequestGivenActorWithoutOidcProviderProof()
    {
        // Arrange
        await using var provider = BuildProvider();

        await RequestScenario.For(provider)
            .GivenActor(RequestActor.Anonymous)
            // Act
            .When(new ContinueWithOidcProvider())
            // Assert
            .ExpectDenied(RequestErrorKind.Unauthorized)
            .ExpectNotHandled();
    }

    [Fact]
    public async Task ShouldHandleRequestGivenActorWithOidcProviderProof()
    {
        // Arrange
        await using var provider = BuildProvider();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "https://issuer.example/"), new Claim("sub", "provider-subject")], "oidc"));

        await RequestScenario.For(provider)
            .GivenActor(actor)
            // Act
            .When(new ContinueWithOidcProvider())
            // Assert
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectSuccess();
    }

    static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddScoped<UserIdentityContinuation>();
        services.AddPortia()
            .AddRequestHandler<ContinueWithOidcProviderHandler>()
            .AddRequestAuthorizer<ContinueWithOidcProviderAuthorizer>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
