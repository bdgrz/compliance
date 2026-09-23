using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class EmailChallengeDeliveryReactorTests
{
    [Fact]
    public async Task ShouldRecordFailureAndDeliverLaterUserGivenSmtpFailure()
    {
        // Arrange
        var owner = Uuid.CreateVersion4();
        var challengeId = Uuid.CreateVersion4();
        const string email = "owner@example.com";
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(15);
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Compliance:EmailDelivery:ActiveTokenKeyId"] = "current",
                ["Compliance:EmailDelivery:TokenKeys:current"] = key,
            }).Build();
        var keys = EmailChallengeTokenKeys.FromConfiguration(configuration, false);
        var token = keys.Derive("current", challengeId, owner, email, expiresAt);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var aggregate = new EmailAddress(email);
        Assert.True(aggregate.Reserve(owner).IsSuccess);
        Assert.True(aggregate.IssueChallenge(owner, challengeId, hash, expiresAt, now, "current").IsSuccess);
        await writer.SaveAsync(aggregate, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        var issued = new EmailChallengeIssued(owner, email, challengeId, hash, expiresAt, "current");
        var delivery = new FailingOnceDelivery();
        var context = new Context(issued);
        var reactor = new EmailChallengeDeliveryReactor(new InMemoryProjectionCheckpointStore(),
            reader, writer, delivery, keys, TimeProvider.System);

        // Act
        await reactor.HandleAsync(context, CancellationToken.None);
        var failed = await reader.HydrateAsync(new EmailAddress(email), CancellationToken.None);
        await reactor.HandleAsync(context, CancellationToken.None);
        var nextOwner = Uuid.CreateVersion4();
        var nextId = Uuid.CreateVersion4();
        const string nextEmail = "later@example.com";
        var nextToken = keys.Derive("current", nextId, nextOwner, nextEmail, expiresAt);
        var nextHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(nextToken)));
        var nextAddress = new EmailAddress(nextEmail);
        Assert.True(nextAddress.Reserve(nextOwner).IsSuccess);
        Assert.True(nextAddress.IssueChallenge(nextOwner, nextId, nextHash, expiresAt,
            now, "current").IsSuccess);
        await writer.SaveAsync(nextAddress, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        await reactor.HandleAsync(new Context(new EmailChallengeIssued(nextOwner,
            nextEmail, nextId, nextHash, expiresAt, "current")), CancellationToken.None);
        var later = await reader.HydrateAsync(new EmailAddress(nextEmail), CancellationToken.None);

        // Assert
        Assert.Equal("failed", failed.DeliveryStatus);
        Assert.Equal("delivered", later.DeliveryStatus);
        Assert.Equal(2, delivery.Attempts.Count);
        Assert.Equal((challengeId, token), delivery.Attempts[0]);
        Assert.Equal((nextId, nextToken), delivery.Attempts[1]);
    }

    [Fact]
    public async Task ShouldRecordFailureWithoutSendingGivenMissingHistoricalKey()
    {
        // Arrange
        var owner = Uuid.CreateVersion4();
        var challengeId = Uuid.CreateVersion4();
        const string email = "owner@example.com";
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(15);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Compliance:EmailDelivery:ActiveTokenKeyId"] = "new",
                ["Compliance:EmailDelivery:TokenKeys:new"] =
                    Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            }).Build();
        var keys = EmailChallengeTokenKeys.FromConfiguration(configuration, false);
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var aggregate = new EmailAddress(email);
        Assert.True(aggregate.Reserve(owner).IsSuccess);
        var hash = new string('A', 64);
        Assert.True(aggregate.IssueChallenge(owner, challengeId, hash, expiresAt, now, "old").IsSuccess);
        await writer.SaveAsync(aggregate, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        var delivery = new FailingOnceDelivery(failFirst: false);
        var reactor = new EmailChallengeDeliveryReactor(new InMemoryProjectionCheckpointStore(),
            reader, writer, delivery, keys, TimeProvider.System);

        // Act
        await reactor.HandleAsync(new Context(new EmailChallengeIssued(owner, email,
            challengeId, hash, expiresAt, "old")), CancellationToken.None);
        var failed = await reader.HydrateAsync(new EmailAddress(email), CancellationToken.None);

        // Assert
        Assert.Equal("failed", failed.GetChallengeStatus(now).DeliveryStatus);
        Assert.Empty(delivery.Attempts);
    }

    [Fact]
    public async Task ShouldRecordFailureWithoutRetryGivenLegacyChallengeWithoutTokenKey()
    {
        // Arrange
        var owner = Uuid.CreateVersion4();
        var challengeId = Uuid.CreateVersion4();
        const string email = "legacy@example.com";
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(15);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>()).Build();
        var keys = EmailChallengeTokenKeys.FromConfiguration(configuration, true);
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var aggregate = new EmailAddress(email);
        Assert.True(aggregate.Reserve(owner).IsSuccess);
        var hash = new string('A', 64);
        Assert.True(aggregate.IssueChallenge(owner, challengeId, hash, expiresAt, now).IsSuccess);
        await writer.SaveAsync(aggregate, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        var delivery = new FailingOnceDelivery();
        var reactor = new EmailChallengeDeliveryReactor(new InMemoryProjectionCheckpointStore(),
            reader, writer, delivery, keys, TimeProvider.System);
        var context = new Context(new EmailChallengeIssued(owner, email, challengeId, hash,
            expiresAt));

        // Act
        await reactor.HandleAsync(context, CancellationToken.None);
        var failed = await reader.HydrateAsync(new EmailAddress(email), CancellationToken.None);
        await reactor.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal("failed", failed.DeliveryStatus);
        Assert.Empty(delivery.Attempts);
    }

    [Fact]
    public async Task ShouldSkipDeliveryGivenExpiredChallenge()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>()).Build();
        var keys = EmailChallengeTokenKeys.FromConfiguration(configuration, true);
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var delivery = new FailingOnceDelivery();
        var reactor = new EmailChallengeDeliveryReactor(new InMemoryProjectionCheckpointStore(),
            scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            scope.ServiceProvider.GetRequiredService<IAggregateWriter>(),
            delivery, keys, TimeProvider.System);
        var expired = new EmailChallengeIssued(Uuid.CreateVersion4(), "owner@example.com",
            Uuid.CreateVersion4(), new string('A', 64), DateTimeOffset.UtcNow.AddSeconds(-1),
            keys.ActiveKeyId);

        // Act
        await reactor.HandleAsync(new Context(expired), CancellationToken.None);

        // Assert
        Assert.Empty(delivery.Attempts);
    }

    sealed class FailingOnceDelivery(bool failFirst = true) : IEmailChallengeDelivery
    {
        public List<(Uuid ChallengeId, string Token)> Attempts { get; } = [];

        public ValueTask SendAsync(Uuid challengeId, Uuid userId, string emailAddress,
            string token, CancellationToken ct)
        {
            _ = userId;
            _ = emailAddress;
            ct.ThrowIfCancellationRequested();
            Attempts.Add((challengeId, token));
            if (failFirst && Attempts.Count == 1)
                throw new InvalidOperationException("provider secret and recipient must stay hidden");
            return ValueTask.CompletedTask;
        }
    }

    sealed class Context(EmailChallengeIssued trigger) : IReactorContext<EmailChallengeIssued>
    {
        public EmailChallengeIssued Trigger { get; } = trigger;
        public DomainEventRecord Source { get; } = new(
            new EventStreamAddress("bdgrz", "email-addresses", Uuid.CreateVersion4().ToString()),
            trigger, 0, EventCursor.Start);
        public ClaimsPrincipal Actor => RequestActor.System;
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid CorrelationId { get; } = Uuid.CreateVersion4();
        public Uuid CauseId { get; } = Uuid.CreateVersion4();
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    }
}
