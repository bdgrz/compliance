using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class RegisterTenantAuthorizerTests
{
    [Fact]
    public async Task ShouldAllowActorWithBdgrzIdentity()
    {
        var authorizer = new RegisterTenantAuthorizer();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));
        var context = new RequestContext<RegisterTenant>(new RegisterTenant("Acme", "acme"), actor);

        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldRejectActorWithoutBdgrzIdentity()
    {
        var authorizer = new RegisterTenantAuthorizer();
        var context = new RequestContext<RegisterTenant>(
            new RegisterTenant("Acme", "acme"),
            new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Unauthorized, result.Error.Kind);
    }
}
