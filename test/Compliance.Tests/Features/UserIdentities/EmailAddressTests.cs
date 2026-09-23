using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class EmailAddressTests
{
    [Fact]
    public void ShouldReserveOnceGivenSameOwnerRetry()
    {
        // Arrange
        var owner = Uuid.CreateVersion4();
        var address = new EmailAddress("Person@Example.com");

        // Act
        var scenario = new AggregateScenario<EmailAddress>(address);

        // Assert
        Assert.True(scenario.Aggregate.Reserve(owner).IsSuccess);
        Assert.True(scenario.Aggregate.Reserve(owner).IsSuccess);

        Assert.Equal("EmailAddressReserved", Assert.Single(scenario.PendingEvents).GetType().Name);
        Assert.False(scenario.Aggregate.IsVerified);
    }

    [Fact]
    public void ShouldRejectTakeoverGivenAnotherUserOwnsAddress()
    {
        // Arrange
        var address = new EmailAddress("person@example.com");

        // Act
        var scenario = new AggregateScenario<EmailAddress>(address);

        // Assert
        Assert.True(scenario.Aggregate.Reserve(Uuid.CreateVersion4()).IsSuccess);

        var conflict = scenario.Aggregate.Reserve(Uuid.CreateVersion4());

        Assert.False(conflict.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, conflict.Error.Kind);
        Assert.Single(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldUseDistinctIdsGivenDistinctAddresses()
    {
        // Arrange
        const string firstAddress = "first@example.com";
        const string secondAddress = "second@example.com";

        // Act
        var first = new EmailAddress(firstAddress).Id;
        var second = new EmailAddress(secondAddress).Id;
        var mixedCase = new EmailAddress("Person@Example.com").Id;
        var lowerCase = new EmailAddress("person@example.com").Id;

        // Assert
        Assert.NotEqual(first, second);
        Assert.Equal(mixedCase, lowerCase);
    }

    [Fact]
    public void ShouldAllowCompletionGivenOwnerAndUnexpiredChallenge()
    {
        // Arrange
        var owner = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var address = new EmailAddress("person@example.com");

        // Act
        var scenario = new AggregateScenario<EmailAddress>(address);

        // Assert
        Assert.True(scenario.Aggregate.Reserve(owner).IsSuccess);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("secret")));
        Assert.True(scenario.Aggregate.IssueChallenge(owner, Uuid.CreateVersion4(), hash, now.AddMinutes(15), now)
            .IsSuccess);

        Assert.False(scenario.Aggregate.CompleteChallenge(Uuid.CreateVersion4(), "secret", now).IsSuccess);
        Assert.False(scenario.Aggregate.CompleteChallenge(owner, "wrong", now).IsSuccess);
        Assert.False(scenario.Aggregate.CompleteChallenge(owner, "secret", now.AddMinutes(15)).IsSuccess);
        Assert.False(scenario.Aggregate.IsVerified);

        Assert.True(scenario.Aggregate.CompleteChallenge(owner, "secret", now.AddMinutes(1)).IsSuccess);
        Assert.True(scenario.Aggregate.IsVerified);
        Assert.Equal(3, scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldInvalidateOldTokenGivenReissuedChallenge()
    {
        // Arrange
        var owner = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var address = new EmailAddress("person@example.com");

        // Act
        var scenario = new AggregateScenario<EmailAddress>(address);

        // Assert
        Assert.True(scenario.Aggregate.Reserve(owner).IsSuccess);
        var oldHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("old")));
        var newHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("new")));
        Assert.True(scenario.Aggregate.IssueChallenge(owner, Uuid.CreateVersion4(), oldHash, now.AddMinutes(15), now)
            .IsSuccess);
        Assert.True(scenario.Aggregate.IssueChallenge(owner, Uuid.CreateVersion4(), newHash, now.AddMinutes(15), now)
            .IsSuccess);

        Assert.False(scenario.Aggregate.CompleteChallenge(owner, "old", now).IsSuccess);
        Assert.True(scenario.Aggregate.CompleteChallenge(owner, "new", now).IsSuccess);
    }

    [Fact]
    public void ShouldTrackDeliveryAndIgnoreSupersededOutcomeGivenReissue()
    {
        // Arrange
        var owner = Uuid.CreateVersion4();
        var firstId = Uuid.CreateVersion4();
        var secondId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var address = new EmailAddress("person@example.com");
        var scenario = new AggregateScenario<EmailAddress>(address);
        Assert.True(address.Reserve(owner).IsSuccess);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("secret")));

        // Act
        Assert.True(address.IssueChallenge(owner, firstId, hash, now.AddMinutes(15), now, "key-1").IsSuccess);
        Assert.Equal("pending", address.GetChallengeStatus(now).DeliveryStatus);
        Assert.True(address.RecordDeliveryFailure(firstId, "delivery_failed", now).IsSuccess);
        Assert.Equal("failed", address.GetChallengeStatus(now).DeliveryStatus);
        var terminal = address.RecordDeliverySent(firstId, now);
        Assert.False(terminal.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(terminal.Error).Kind);
        Assert.Equal("failed", address.GetChallengeStatus(now).DeliveryStatus);
        Assert.True(address.IssueChallenge(owner, secondId, hash, now.AddMinutes(15), now, "key-1").IsSuccess);
        var beforeStale = scenario.PendingEvents.Count;
        Assert.True(address.RecordDeliverySent(firstId, now).IsSuccess);

        // Assert
        Assert.Equal(beforeStale, scenario.PendingEvents.Count);
        Assert.Equal("pending", address.GetChallengeStatus(now).DeliveryStatus);
        Assert.Equal(secondId, address.CurrentChallengeId);
        Assert.Equal("expired", address.GetChallengeStatus(now.AddMinutes(15)).DeliveryStatus);
    }
}
