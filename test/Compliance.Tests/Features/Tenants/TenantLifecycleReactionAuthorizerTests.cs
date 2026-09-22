using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantLifecycleReactionAuthorizerTests
{
    public static TheoryData<ITenantLifecycleReactionRequest> Requests =>
    [
        new RegisterTenantSlug(Uuid.CreateVersion4(), "acme"),
        new RegisterTenantOwner(Uuid.CreateVersion4(), Uuid.CreateVersion4()),
        new ConfirmTenantSlug(Uuid.CreateVersion4(), "acme"),
        new RejectTenantSlug(Uuid.CreateVersion4(), "acme"),
        new SurrenderTenantSlug(Uuid.CreateVersion4(), "acme"),
        new ConfirmTenantSlugSurrender(Uuid.CreateVersion4(), "acme"),
        new RejectTenantSlugSurrender(Uuid.CreateVersion4(), "acme"),
    ];

    [Theory]
    [MemberData(nameof(Requests))]
    public async Task ShouldAllowReactionRequestGivenSystemActor(ITenantLifecycleReactionRequest request)
    {
        // Arrange
        var authorizer = new TenantLifecycleReactionAuthorizer();
        var context = new RequestContext<ITenantLifecycleReactionRequest>(request, RequestActor.System);

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [MemberData(nameof(Requests))]
    public async Task ShouldRejectReactionRequestGivenNonSystemActor(ITenantLifecycleReactionRequest request)
    {
        // Arrange
        var authorizer = new TenantLifecycleReactionAuthorizer();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));
        var context = new RequestContext<ITenantLifecycleReactionRequest>(request, actor);

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }
}
