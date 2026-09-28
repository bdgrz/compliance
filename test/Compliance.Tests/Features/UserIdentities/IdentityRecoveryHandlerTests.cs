using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class IdentityRecoveryHandlerTests
{
    [Fact]
    public async Task ShouldReturnSameSuccessGivenUnknownOrUnverifiedAddress()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        const string unverifiedEmail = "unverified@example.com";
        var address = new EmailAddress(unverifiedEmail);
        Assert.True(address.Reserve(Uuid.CreateVersion4()).IsSuccess);
        await fixture.Repository.SaveAsync(address, new DispatchContext(), CancellationToken.None);
        var handler = new StartIdentityRecoveryHandler(fixture.Repository, CreateTokenKeys(),
            TimeProvider.System);
        var actor = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var unknown = await handler.HandleAsync(Context(new StartIdentityRecovery("unknown@example.com"),
            actor), CancellationToken.None);
        var unverified = await handler.HandleAsync(Context(new StartIdentityRecovery(unverifiedEmail),
            actor), CancellationToken.None);
        var persisted = await fixture.Repository.HydrateAsync(
            new EmailAddress(unverifiedEmail), CancellationToken.None);

        // Assert
        Assert.Equal(unknown.IsSuccess, unverified.IsSuccess);
        Assert.True(unknown.IsSuccess);
        Assert.Null(persisted.CurrentRecoveryChallengeId);
    }

    [Fact]
    public async Task ShouldReplaceOneIdentityGivenVerifiedEmailAndNewProviderProof()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var userId = Uuid.CreateVersion4();
        const string email = "person@example.com";
        var old = new UserIdentity("https://old-issuer.example/", "old-subject");
        Assert.True(old.Register(userId, email).IsSuccess);
        await fixture.Repository.SaveAsync(old, new DispatchContext(), CancellationToken.None);
        await SeedVerifiedAddress(fixture.Repository, userId, email);
        var keys = CreateTokenKeys();
        var start = new StartIdentityRecoveryHandler(fixture.Repository, keys, TimeProvider.System);
        var startContext = Context(new StartIdentityRecovery(email), new ClaimsPrincipal(new ClaimsIdentity()));

        // Act
        var started = await start.HandleAsync(startContext, CancellationToken.None);
        var repeatedStart = await start.HandleAsync(startContext, CancellationToken.None);
        var address = await fixture.Repository.HydrateAsync(new EmailAddress(email), CancellationToken.None);
        var challengeId = Assert.IsType<Uuid>(address.CurrentRecoveryChallengeId);
        var repeatedChallengeId = address.CurrentRecoveryChallengeId;
        var issued = await ReadEvent<IdentityRecoveryChallengeIssued>(fixture.Store, email);
        var token = keys.DeriveRecovery(issued.TokenKeyId, challengeId, userId, email, issued.ExpiresAt);
        var emailVerificationToken = keys.Derive(issued.TokenKeyId, challengeId,
            userId, email, issued.ExpiresAt);
        var actor = ProviderActor("https://new-issuer.example/", "new-subject");
        var optionsHandler = new GetIdentityRecoveryOptionsHandler(fixture.Repository,
            new TestIdentityDirectoryReader([new UserIdentityDirectoryEntry(userId,
                old.Id, "https://old-issuer.example/", IsRevoked: false)]),
            new CaughtUpIdentityDirectory(), TimeProvider.System);
        var options = await optionsHandler.HandleAsync(Context(
            new GetIdentityRecoveryOptions(email, challengeId, token), actor), CancellationToken.None);
        var request = new CompleteIdentityRecovery(email, challengeId, token, old.Id);
        var handler = new CompleteIdentityRecoveryHandler(fixture.Repository, fixture.Repository,
            TimeProvider.System);
        var context = Context(request, actor);
        var completed = await handler.HandleAsync(context, CancellationToken.None);
        var replayed = await handler.HandleAsync(context, CancellationToken.None);
        var replacement = await fixture.Repository.HydrateAsync(
            new UserIdentity("https://new-issuer.example/", "new-subject"), CancellationToken.None);
        var retired = await fixture.Repository.HydrateAsync(new UserIdentity(old.Id), CancellationToken.None);
        address = await fixture.Repository.HydrateAsync(new EmailAddress(email), CancellationToken.None);

        // Assert
        Assert.True(started.IsSuccess);
        Assert.True(repeatedStart.IsSuccess);
        Assert.Equal(challengeId, repeatedChallengeId);
        Assert.NotEqual(emailVerificationToken, token);
        Assert.True(options.IsSuccess);
        Assert.Equal(1, options.Value.ProjectionRevision);
        var option = Assert.Single(options.Value.Identities.Items);
        Assert.Equal(old.Id, option.UserIdentityId);
        Assert.Equal("https://old-issuer.example/", option.Provider);
        Assert.True(completed.IsSuccess);
        Assert.True(replayed.IsSuccess);
        Assert.Equal(userId, replacement.UserId);
        Assert.True(retired.IsRevoked);
        Assert.True(address.IsRecoveryCompleted);
        Assert.Equal(old.Id, address.RecoveryOldIdentityId);
        Assert.Equal(replacement.Id, address.RecoveryReplacementIdentityId);
        Assert.False(retired.Authenticate().IsSuccess);
        Assert.True(replacement.Authenticate().IsSuccess);
        var records = new List<DomainEvent>();
        await foreach (var record in fixture.Store.ReadAsync(
                           EventStreamPattern.ForPattern("bdgrz", "user-identities"),
                           EventCursor.Start, CancellationToken.None))
            records.Add(record.Event);
        Assert.Single(records.OfType<UserIdentityRevoked>());
        Assert.Equal(userId, Assert.Single(records.OfType<UserIdentityRegistered>(),
            registered => registered.Identifier == "new-subject").UserId);
    }

    [Fact]
    public async Task ShouldRejectRetargetedRecoveryGivenChallengeAlreadyClaimed()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var userId = Uuid.CreateVersion4();
        const string email = "person@example.com";
        var old = new UserIdentity("https://old-issuer.example/", "old-subject");
        Assert.True(old.Register(userId, email).IsSuccess);
        await fixture.Repository.SaveAsync(old, new DispatchContext(), CancellationToken.None);
        await SeedVerifiedAddress(fixture.Repository, userId, email);
        var keys = CreateTokenKeys();
        var start = new StartIdentityRecoveryHandler(fixture.Repository, keys, TimeProvider.System);
        await start.HandleAsync(Context(new StartIdentityRecovery(email),
            new ClaimsPrincipal(new ClaimsIdentity())), CancellationToken.None);
        var issued = await ReadEvent<IdentityRecoveryChallengeIssued>(fixture.Store, email);
        var token = keys.DeriveRecovery(issued.TokenKeyId, issued.ChallengeId,
            userId, email, issued.ExpiresAt);
        var first = new CompleteIdentityRecoveryHandler(fixture.Repository, fixture.Repository,
            TimeProvider.System);
        var firstRequest = new CompleteIdentityRecovery(email, issued.ChallengeId, token, old.Id);
        var firstResult = await first.HandleAsync(Context(firstRequest,
            ProviderActor("https://new-issuer.example/", "new-subject")), CancellationToken.None);

        // Act
        var retargeted = await first.HandleAsync(Context(firstRequest,
            ProviderActor("https://attacker-issuer.example/", "other-subject")), CancellationToken.None);

        // Assert
        Assert.True(firstResult.IsSuccess);
        Assert.False(retargeted.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, retargeted.Error.Kind);
        var other = await fixture.Repository.HydrateAsync(
            new UserIdentity("https://attacker-issuer.example/", "other-subject"), CancellationToken.None);
        Assert.False(other.IsRegistered);
    }

    [Fact]
    public async Task ShouldRejectStaleIdentityDirectoryGivenMinimumProjectionRevision()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var userId = Uuid.CreateVersion4();
        const string email = "person@example.com";
        const string provider = "https://old-issuer.example/";
        var identity = new UserIdentity(provider, "old-subject");
        Assert.True(identity.Register(userId, email).IsSuccess);
        await fixture.Repository.SaveAsync(identity, new DispatchContext(), CancellationToken.None);
        await SeedVerifiedAddress(fixture.Repository, userId, email);
        var tokenKeys = CreateTokenKeys();
        await new StartIdentityRecoveryHandler(fixture.Repository, tokenKeys, TimeProvider.System)
            .HandleAsync(Context(new StartIdentityRecovery(email),
                new ClaimsPrincipal(new ClaimsIdentity())), CancellationToken.None);
        var issued = await ReadEvent<IdentityRecoveryChallengeIssued>(fixture.Store, email);
        var token = tokenKeys.DeriveRecovery(issued.TokenKeyId, issued.ChallengeId,
            userId, email, issued.ExpiresAt);
        var handler = new GetIdentityRecoveryOptionsHandler(fixture.Repository,
            new TestIdentityDirectoryReader([new UserIdentityDirectoryEntry(userId,
                identity.Id, provider, IsRevoked: false)]),
            new CaughtUpIdentityDirectory(), TimeProvider.System);

        // Act
        var result = await handler.HandleAsync(Context(new GetIdentityRecoveryOptions(email,
            issued.ChallengeId, token, MinimumProjectionRevision: 2),
            ProviderActor("https://new-issuer.example/", "new-subject")), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(result.Error.IsTransient);
    }

    static async Task SeedVerifiedAddress(AggregateCapabilities writer, Uuid userId, string email)
    {
        var now = DateTimeOffset.UtcNow;
        var token = "verified-email-proof";
        var address = new EmailAddress(email);
        Assert.True(address.Reserve(userId).IsSuccess);
        Assert.True(address.IssueChallenge(userId, Uuid.CreateVersion4(),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
            now.AddMinutes(15), now).IsSuccess);
        Assert.True(address.CompleteChallenge(userId, token, now).IsSuccess);
        await writer.SaveAsync(address, new DispatchContext(), CancellationToken.None);
    }

    static EmailChallengeTokenKeys CreateTokenKeys()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Compliance:EmailDelivery:ActiveTokenKeyId"] = "current",
                ["Compliance:EmailDelivery:TokenKeys:current"] =
                    Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            }).Build();
        return EmailChallengeTokenKeys.FromConfiguration(configuration, false);
    }

    static async Task<TEvent> ReadEvent<TEvent>(IEventStore store, string email)
        where TEvent : DomainEvent
    {
        await foreach (var record in store.ReadAsync(new EmailAddress(email).Stream,
                           0, CancellationToken.None))
        {
            if (record.Event is TEvent domainEvent)
                return domainEvent;
        }
        throw new Xunit.Sdk.XunitException($"No {typeof(TEvent).Name} event was recorded.");
    }

    static ClaimsPrincipal ProviderActor(string issuer, string subject) => new(
        new ClaimsIdentity([new Claim("iss", issuer), new Claim("sub", subject)], "oidc"));

    static RequestContext<TRequest> Context<TRequest>(TRequest request, ClaimsPrincipal actor) =>
        new(request, actor);

    sealed class DispatchContext : IExecutionContext
    {
        public ClaimsPrincipal Actor { get; } = new(new ClaimsIdentity());
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid CorrelationId { get; } = Uuid.CreateVersion4();
        public Uuid CauseId { get; } = Uuid.CreateVersion4();
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    }

    sealed class TestIdentityDirectoryReader(IReadOnlyList<UserIdentityDirectoryEntry> identities)
        : IUserIdentityDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(ProjectionCheckpoint.Start);

        public ValueTask<UserIdentityDirectoryPage> ListAsync(Uuid userId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new UserIdentityDirectoryPage(1,
                new Page<UserIdentityDirectoryEntry>(identities
                    .Where(identity => identity.UserId == userId && !identity.IsRevoked)
                    .Take(limit).ToArray(), null)));
    }

    sealed class CaughtUpIdentityDirectory : IUserIdentityDirectoryReadConsistency
    {
        public ValueTask<Result> EnsureCaughtUpAsync(CancellationToken ct) =>
            ValueTask.FromResult(Result.Success);
    }
}
