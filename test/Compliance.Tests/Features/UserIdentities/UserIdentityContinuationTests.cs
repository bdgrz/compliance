using System.Globalization;
using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class UserIdentityContinuationTests
{
    [Fact]
    public async Task ShouldProduceSameIdentityShapeGivenDeveloperAndOidcRegistration()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var continuation = new UserIdentityContinuation(fixture.Repository);
        var developerHandler = new ContinueWithDeveloperIdentityHandler(
            continuation,
            new DeveloperUserRegistration(true));
        var oidcHandler = new ContinueWithOidcProviderHandler(continuation);
        var developerContext = Context(
            new ContinueWithDeveloperIdentity("Person@Example.com"),
            new ClaimsPrincipal(new ClaimsIdentity()));
        var oidcContext = Context(
            new ContinueWithOidcProvider(),
            AuthenticatedActor(
                new Claim("iss", "https://issuer.example/"),
                new Claim("sub", "provider-subject"),
                new Claim("email", "Person@Example.com")));

        // Act
        var developer = await developerHandler.HandleAsync(developerContext, CancellationToken.None);
        var oidc = await oidcHandler.HandleAsync(oidcContext, CancellationToken.None);
        var developerIdentity = await fixture.Repository.HydrateAsync(
            new UserIdentity(
                DeveloperUserRegistration.Provider,
                Uuid.CreateVersion5(
                    DeveloperUserRegistration.IdentifierNamespaceId,
                    "person@example.com").ToString()),
            CancellationToken.None);
        var oidcIdentity = await fixture.Repository.HydrateAsync(
            new UserIdentity("https://issuer.example/", "provider-subject"),
            CancellationToken.None);
        var developerEventType = await ReadEventType(fixture.Store, developerIdentity.Stream);
        var oidcEventType = await ReadEventType(fixture.Store, oidcIdentity.Stream);

        // Assert
        Assert.True(developer.IsSuccess);
        Assert.True(oidc.IsSuccess);
        Assert.Equal("person@example.com", developer.Value.EmailAddress);
        Assert.Equal("person@example.com", oidc.Value.EmailAddress);
        Assert.NotEqual(Uuid.Empty, developer.Value.UserId);
        Assert.NotEqual(Uuid.Empty, oidc.Value.UserId);
        Assert.Equal(developerIdentity.Id, developer.Value.UserIdentityId);
        Assert.Equal(oidcIdentity.Id, oidc.Value.UserIdentityId);
        Assert.Equal(typeof(UserIdentityRegistered), developerEventType);
        Assert.Equal(developerEventType, oidcEventType);
    }

    [Fact]
    public async Task ShouldRegisterWithoutEmailGivenOidcProviderOmitsClaim()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var handler = new ContinueWithOidcProviderHandler(new UserIdentityContinuation(fixture.Repository));
        var context = Context(
            new ContinueWithOidcProvider(),
            AuthenticatedActor(
                new Claim("iss", "https://issuer.example/"),
                new Claim("sub", "no-email-subject")));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);
        var identity = await fixture.Repository.HydrateAsync(
            new UserIdentity("https://issuer.example/", "no-email-subject"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.EmailAddress);
        Assert.Equal(identity.Id, result.Value.UserIdentityId);
    }

    [Fact]
    public async Task ShouldReuseExistingUserGivenAdditionalOidcProvider()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var handler = new ContinueWithOidcProviderHandler(new UserIdentityContinuation(fixture.Repository));
        var existingUserId = Uuid.Parse(
            "a85d59b8-2f35-42b0-bc02-03085ad7d20f",
            CultureInfo.InvariantCulture);
        var externalIdentity = new ClaimsIdentity(
            new[]
            {
                new Claim("iss", "https://another-issuer.example/"),
                new Claim("sub", "linked-subject"),
            },
            "oidc");
        var sessionIdentity = new ClaimsIdentity(
            new[]
            {
                new Claim("iss", "bdgrz"),
                new Claim("sub", existingUserId.ToString()),
            },
            "BdgrzSession");
        var context = Context(
            new ContinueWithOidcProvider(),
            new ClaimsPrincipal(new[] { externalIdentity, sessionIdentity }));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(existingUserId, result.Value.UserId);
    }

    [Fact]
    public async Task ShouldRegisterThenAuthenticateGivenSameOidcProviderIdentity()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var handler = new ContinueWithOidcProviderHandler(new UserIdentityContinuation(fixture.Repository));
        var actor = AuthenticatedActor(
            new Claim("iss", "https://issuer.example/"),
            new Claim("sub", "returning-subject"),
            new Claim("email", "person@example.com"));

        // Act
        var registration = await handler.HandleAsync(
            Context(new ContinueWithOidcProvider(), actor),
            CancellationToken.None);
        var authentication = await handler.HandleAsync(
            Context(new ContinueWithOidcProvider(), actor),
            CancellationToken.None);
        var records = new List<DomainEventRecord>();
        await foreach (var record in fixture.Store.ReadAsync(
                           EventStreamPattern.ForPattern("bdgrz", "user-identities"),
                           EventCursor.Start,
                           CancellationToken.None))
        {
            records.Add(record);
        }

        // Assert
        Assert.True(registration.IsSuccess);
        Assert.True(authentication.IsSuccess);
        Assert.Equal(registration.Value.UserId, authentication.Value.UserId);
        Assert.Equal(
            ["UserIdentityRegistered", "UserIdentityAuthenticated"],
            records.Select(record => record.Event.GetType().Name));
        Assert.False(records[0].Event.Metadata.IsAudit);
        Assert.True(records[1].Event.Metadata.IsAudit);
    }

    static RequestContext<TRequest> Context<TRequest>(TRequest request, ClaimsPrincipal actor) =>
        new(request, actor);

    static ClaimsPrincipal AuthenticatedActor(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "oidc"));

    static async Task<Type> ReadEventType(
        IEventStore store,
        EventStreamAddress stream)
    {
        await foreach (var record in store.ReadAsync(stream, 0, CancellationToken.None))
        {
            return record.Event.GetType();
        }

        throw new Xunit.Sdk.XunitException("The identity registration event was not persisted.");
    }

    sealed class RequestContext<TRequest>(TRequest request, ClaimsPrincipal actor) : IRequestContext<TRequest>
    {
        public TRequest Request { get; } = request;
        public ClaimsPrincipal Actor => actor;
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid RequestId { get; } = Uuid.CreateVersion4();
        public Uuid CorrelationId { get; } = Uuid.CreateVersion4();
        public Uuid? CausationId => null;
        public Uuid CauseId => RequestId;
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
        public RequestInvocation Invocation { get; } = new DirectInvocation();
    }
}
