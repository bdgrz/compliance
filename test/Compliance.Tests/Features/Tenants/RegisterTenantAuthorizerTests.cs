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
        var authorizer = new RegisterTenantAuthorizer(new PlatformOperatorAuthority([]),
            new EmailDirectory(userId, true));
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));
        var context = new RequestContext<RegisterTenant>(
            new RegisterTenant("Acme", "acme", "Acme LLC", "creator@example.com"), actor);

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldRejectActorGivenMissingBdgrzIdentity()
    {
        // Arrange
        var authorizer = new RegisterTenantAuthorizer(new PlatformOperatorAuthority([]),
            new EmailDirectory(null, false));
        var context = new RequestContext<RegisterTenant>(
            new RegisterTenant("Acme", "acme"),
            new ClaimsPrincipal(new ClaimsIdentity()));

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Unauthorized, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldRejectUserGivenUnverifiedEmail()
    {
        // Arrange
        var userId = Uuid.CreateVersion4();
        var authorizer = new RegisterTenantAuthorizer(new PlatformOperatorAuthority([]),
            new EmailDirectory(userId, false));
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

        // Act
        var result = await authorizer.AuthorizeAsync(
            new RequestContext<RegisterTenant>(
                new RegisterTenant("Acme", "acme", "Acme LLC", "creator@example.com"), actor),
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldRejectUserGivenVerifiedEmailOwnedByAnotherUser()
    {
        // Arrange
        var authorizer = new RegisterTenantAuthorizer(new PlatformOperatorAuthority([]),
            new EmailDirectory(Uuid.CreateVersion4(), true));
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));

        // Act
        var result = await authorizer.AuthorizeAsync(new RequestContext<RegisterTenant>(
            new RegisterTenant("Acme", "acme", "Acme LLC", "creator@example.com"), actor),
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }

    sealed class EmailDirectory(Uuid? owner, bool verified) : IEmailAddressDirectoryReader
    {
        public ValueTask<EmailAddressView?> GetAsync(string emailAddress,
            CancellationToken ct = default) => ValueTask.FromResult<EmailAddressView?>(
            owner is { } userId ? new EmailAddressView(userId, emailAddress, verified) : null);

        public ValueTask<Page<EmailAddressView>> ListAsync(Uuid userId, int? limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<EmailAddressView>([], null));
    }
}
