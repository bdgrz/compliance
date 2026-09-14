using System.Globalization;
using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests;

public sealed class UserIdentityRegistrationTests
{
    [Fact]
    public async Task ShouldProduceSameIdentityShapeGivenDeveloperAndOidcRegistration()
    {
        // Arrange
        var store = new InMemoryEventStore();
        var repository = new AggregateRepository(store);
        var registration = new UserIdentityRegistration(repository);
        var developerHandler = new RegisterDeveloperUserHandler(
            registration,
            new DeveloperUserRegistration(true));
        var oidcHandler = new RegisterOidcUserHandler(registration);
        var developerContext = Context(
            new RegisterDeveloperUser("Person@Example.com"),
            new ClaimsPrincipal(new ClaimsIdentity()));
        var oidcContext = Context(
            new RegisterOidcUser(),
            AuthenticatedActor(
                new Claim("iss", "https://issuer.example/"),
                new Claim("sub", "provider-subject"),
                new Claim("email", "Person@Example.com")));

        // Act
        var developer = await developerHandler.HandleAsync(developerContext, CancellationToken.None);
        var oidc = await oidcHandler.HandleAsync(oidcContext, CancellationToken.None);
        var developerIdentity = await repository.HydrateAsync(
            new UserIdentity(developer.Value.UserIdentityId),
            CancellationToken.None);
        var oidcIdentity = await repository.HydrateAsync(
            new UserIdentity(oidc.Value.UserIdentityId),
            CancellationToken.None);
        var developerEventType = await ReadEventType(store, developerIdentity.Stream);
        var oidcEventType = await ReadEventType(store, oidcIdentity.Stream);

        // Assert
        Assert.True(developer.IsSuccess);
        Assert.True(oidc.IsSuccess);
        Assert.Equal("person@example.com", developerIdentity.EmailAddress);
        Assert.Equal("person@example.com", oidcIdentity.EmailAddress);
        Assert.NotEqual(Uuid.Empty, developerIdentity.UserId);
        Assert.NotEqual(Uuid.Empty, oidcIdentity.UserId);
        Assert.Equal(DeveloperUserRegistration.Provider, developerIdentity.Provider);
        Assert.Equal("https://issuer.example/", oidcIdentity.Provider);
        Assert.Equal(typeof(UserIdentityRegistered), developerEventType);
        Assert.Equal(developerEventType, oidcEventType);
    }

    [Fact]
    public async Task ShouldRegisterWithoutEmailGivenOidcProviderOmitsClaim()
    {
        // Arrange
        var repository = new AggregateRepository(new InMemoryEventStore());
        var handler = new RegisterOidcUserHandler(new UserIdentityRegistration(repository));
        var context = Context(
            new RegisterOidcUser(),
            AuthenticatedActor(
                new Claim("iss", "https://issuer.example/"),
                new Claim("sub", "no-email-subject")));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);
        var identity = await repository.HydrateAsync(
            new UserIdentity(result.Value.UserIdentityId),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.EmailAddress);
        Assert.Null(identity.EmailAddress);
    }

    [Fact]
    public async Task ShouldReuseExistingUserGivenAdditionalOidcProvider()
    {
        // Arrange
        var repository = new AggregateRepository(new InMemoryEventStore());
        var handler = new RegisterOidcUserHandler(new UserIdentityRegistration(repository));
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
            new RegisterOidcUser(),
            new ClaimsPrincipal(new[] { externalIdentity, sessionIdentity }));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(existingUserId, result.Value.UserId);
    }

    static RequestContext<TRequest> Context<TRequest>(TRequest request, ClaimsPrincipal actor) =>
        new(request, actor);

    static ClaimsPrincipal AuthenticatedActor(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "oidc"));

    static async Task<Type> ReadEventType(
        InMemoryEventStore store,
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
