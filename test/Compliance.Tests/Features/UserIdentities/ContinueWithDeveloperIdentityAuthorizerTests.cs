using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class ContinueWithDeveloperIdentityAuthorizerTests
{
    [Fact]
    public async Task ShouldAllowRequestGivenEnabledDeveloperAuthentication()
    {
        // Arrange
        var authorizer = new ContinueWithDeveloperIdentityAuthorizer(new DeveloperUserRegistration(true));
        var context = new RequestContext<ContinueWithDeveloperIdentity>(
            new ContinueWithDeveloperIdentity("person@example.com"), RequestActor.Anonymous);

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldRejectRequestGivenDisabledDeveloperAuthentication()
    {
        // Arrange
        var authorizer = new ContinueWithDeveloperIdentityAuthorizer(new DeveloperUserRegistration(false));
        var context = new RequestContext<ContinueWithDeveloperIdentity>(
            new ContinueWithDeveloperIdentity("person@example.com"), RequestActor.Anonymous);

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }
}
