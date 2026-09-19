using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class RegisterTenantAuthorizerTests
{
    [Fact]
    public async Task ShouldAllowActorGivenBdgrzIdentity()
    {
        // Arrange
        var userId = Uuid.CreateVersion4();
        var authorizer = new PlatformOperatorAuthorizer(new PlatformOperatorAuthority([userId]));
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));
        var context = new RequestContext<IPlatformOperatorRequest>(new RegisterTenant("Acme", "acme"), actor);

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldRejectActorGivenMissingBdgrzIdentity()
    {
        // Arrange
        var authorizer = new PlatformOperatorAuthorizer(new PlatformOperatorAuthority([]));
        var context = new RequestContext<IPlatformOperatorRequest>(
            new RegisterTenant("Acme", "acme"),
            new ClaimsPrincipal(new ClaimsIdentity()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Unauthorized, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldRejectUserGivenMissingOperatorGrant()
    {
        // Arrange
        var authorizer = new PlatformOperatorAuthorizer(new PlatformOperatorAuthority([]));
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));

        // Act
        var result = await authorizer.AuthorizeAsync(
            new RequestContext<IPlatformOperatorRequest>(new RegisterTenant("Acme", "acme"), actor),
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }
}
