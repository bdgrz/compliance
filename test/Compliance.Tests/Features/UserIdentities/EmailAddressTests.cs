using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class EmailAddressTests
{
    [Fact]
    public void ReservationIsIdempotentForItsOwner()
    {
        var owner = Uuid.CreateVersion4();
        var address = new EmailAddress("Person@Example.com");
        var scenario = new AggregateScenario<EmailAddress>(address);

        Assert.True(scenario.Aggregate.Reserve(owner).IsSuccess);
        Assert.True(scenario.Aggregate.Reserve(owner).IsSuccess);

        Assert.Equal("EmailAddressReserved", Assert.Single(scenario.PendingEvents).GetType().Name);
        Assert.False(scenario.Aggregate.IsVerified);
    }

    [Fact]
    public void AnotherUserCannotTakeAnAddress()
    {
        var address = new EmailAddress("person@example.com");
        var scenario = new AggregateScenario<EmailAddress>(address);
        Assert.True(scenario.Aggregate.Reserve(Uuid.CreateVersion4()).IsSuccess);

        var conflict = scenario.Aggregate.Reserve(Uuid.CreateVersion4());

        Assert.False(conflict.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, conflict.Error.Kind);
        Assert.Single(scenario.PendingEvents);
    }

    [Fact]
    public void DistinctAddressesHaveDistinctAggregateIds()
    {
        Assert.NotEqual(new EmailAddress("first@example.com").Id, new EmailAddress("second@example.com").Id);
        Assert.Equal(new EmailAddress("Person@Example.com").Id, new EmailAddress("person@example.com").Id);
    }

    [Fact]
    public void OnlyTheOwnerCanCompleteAnUnexpiredChallenge()
    {
        var owner = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var address = new EmailAddress("person@example.com");
        var scenario = new AggregateScenario<EmailAddress>(address);
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
    public void ReissuedChallengeInvalidatesThePreviousToken()
    {
        var owner = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var address = new EmailAddress("person@example.com");
        var scenario = new AggregateScenario<EmailAddress>(address);
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
}
