using System.Security.Claims;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class IdentityRecoveryNoticeReactorTests
{
    [Fact]
    public async Task ShouldRetryCompletionNoticeGivenTransientDeliveryFailure()
    {
        // Arrange
        var challengeId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        const string email = "owner@example.com";
        var delivery = new RecordingDelivery();
        var reactor = new IdentityRecoveryNoticeReactor(
            new InMemoryProjectionCheckpointStore(), delivery);
        var context = new Context(new IdentityRecoveryChallengeCompleted(
            userId, email, challengeId, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            DateTimeOffset.UtcNow));

        // Act
        var first = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await reactor.HandleAsync(context, CancellationToken.None));
        await reactor.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal("Identity recovery notice delivery failed.", first.Message);
        Assert.Equal([challengeId, challengeId], delivery.ChallengeIds);
        Assert.Equal((userId, email), delivery.LastRecipient);
    }

    sealed class RecordingDelivery : IEmailChallengeDelivery
    {
        bool _fail = true;

        public List<Uuid> ChallengeIds { get; } = [];
        public (Uuid UserId, string EmailAddress) LastRecipient { get; private set; }

        public ValueTask SendAsync(Uuid challengeId, Uuid userId, string emailAddress,
            string token, CancellationToken ct)
        {
            _ = challengeId;
            _ = userId;
            _ = emailAddress;
            _ = token;
            ct.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }

        public ValueTask SendRecoveryAsync(Uuid challengeId, Uuid userId,
            string emailAddress, string token, CancellationToken ct) =>
            SendAsync(challengeId, userId, emailAddress, token, ct);

        public ValueTask SendRecoveryCompletedAsync(Uuid challengeId, Uuid userId,
            string emailAddress, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            ChallengeIds.Add(challengeId);
            LastRecipient = (userId, emailAddress);
            if (_fail)
            {
                _fail = false;
                throw new InvalidOperationException("recipient and relay details must not escape");
            }
            return ValueTask.CompletedTask;
        }
    }

    sealed class Context(IdentityRecoveryChallengeCompleted trigger)
        : IReactorContext<IdentityRecoveryChallengeCompleted>
    {
        public IdentityRecoveryChallengeCompleted Trigger { get; } = trigger;
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
