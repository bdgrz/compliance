using System.Globalization;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class MemberLifecycleTests
{
    static readonly Uuid TenantId = Uuid.Parse("11f455d2-fb10-4f28-a157-23e18e706e70", CultureInfo.InvariantCulture);
    static readonly Uuid UserId = Uuid.Parse("0862062f-97e9-45de-a312-0f884c48180d", CultureInfo.InvariantCulture);
    static readonly Uuid ActorMemberId = Uuid.Parse("2cc8e854-a49a-42a1-9581-d0622de3d5c3", CultureInfo.InvariantCulture);
    static readonly DateTimeOffset ActionAt = new(2026, 9, 29, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRecordAttributedSuspensionGivenRegisteredMember()
    {
        // Arrange
        var member = new Member(TenantId, UserId);
        var scenario = new AggregateScenario<Member>(member)
            .Given(DomainEventSeed.Attach(new MemberRegistered(TenantId, member.Id, UserId), member.Id, 1));

        // Act
        var result = scenario.Aggregate.Suspend(ActorMemberId, "Alex Admin", ActionAt,
            "Employment ended.");

        // Assert
        Assert.True(result.IsSuccess);
        var suspended = Assert.IsType<MemberSuspended>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(TenantId, suspended.TenantId);
        Assert.Equal(member.Id, suspended.MemberId);
        Assert.Equal(UserId, suspended.UserId);
        Assert.Equal(ActorMemberId, suspended.SuspendedByMemberId);
        Assert.Equal("Alex Admin", suspended.SuspendedByDisplay);
        Assert.Equal(ActionAt, suspended.SuspendedAt);
        Assert.Equal("Employment ended.", suspended.Reason);
    }

    [Fact]
    public void ShouldRecordAttributedReinstatementGivenSuspendedMember()
    {
        // Arrange
        var member = new Member(TenantId, UserId);
        var scenario = new AggregateScenario<Member>(member).Given(
            DomainEventSeed.Attach(new MemberRegistered(TenantId, member.Id, UserId), member.Id, 1),
            DomainEventSeed.Attach(new MemberSuspended(TenantId, member.Id, UserId, ActorMemberId,
                "Alex Admin", ActionAt, "Employment ended."), member.Id, 2));

        // Act
        var result = scenario.Aggregate.Reinstate(ActorMemberId, "Alex Admin", ActionAt.AddHours(1));

        // Assert
        Assert.True(result.IsSuccess);
        var reinstated = Assert.IsType<MemberReinstated>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(TenantId, reinstated.TenantId);
        Assert.Equal(member.Id, reinstated.MemberId);
        Assert.Equal(UserId, reinstated.UserId);
        Assert.Equal("client_personnel", reinstated.Affiliation);
        Assert.Equal(ActorMemberId, reinstated.ReinstatedByMemberId);
        Assert.Equal("Alex Admin", reinstated.ReinstatedByDisplay);
        Assert.Equal(ActionAt.AddHours(1), reinstated.ReinstatedAt);
    }

    [Fact]
    public void ShouldTerminateMembershipEpisodeGivenRegisteredMember()
    {
        // Arrange
        var member = new Member(TenantId, UserId);
        var scenario = new AggregateScenario<Member>(member)
            .Given(DomainEventSeed.Attach(new MemberRegistered(TenantId, member.Id, UserId), member.Id, 1));

        // Act
        var result = scenario.Aggregate.Deprovision(ActorMemberId, "Alex Admin", ActionAt,
            "Access is no longer required.");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(scenario.Aggregate.IsRegistered);
        Assert.True(scenario.Aggregate.IsDeprovisioned);
        var deprovisioned = Assert.IsType<MemberDeprovisioned>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(TenantId, deprovisioned.TenantId);
        Assert.Equal(member.Id, deprovisioned.MemberId);
        Assert.Equal(UserId, deprovisioned.UserId);
        Assert.Equal(ActorMemberId, deprovisioned.DeprovisionedByMemberId);
        Assert.Equal("Alex Admin", deprovisioned.DeprovisionedByDisplay);
        Assert.Equal(ActionAt, deprovisioned.DeprovisionedAt);
        Assert.Equal("Access is no longer required.", deprovisioned.Reason);
    }

    [Fact]
    public void ShouldStartFreshMembershipEpisodeGivenDeprovisionedMember()
    {
        // Arrange
        var member = new Member(TenantId, UserId);
        var priorEpisodeId = Uuid.CreateVersion4();
        var scenario = new AggregateScenario<Member>(member).Given(
            DomainEventSeed.Attach(new MemberRegistered(TenantId, member.Id, UserId,
                "client_personnel", priorEpisodeId), member.Id, 1),
            DomainEventSeed.Attach(new MemberDeprovisioned(TenantId, member.Id, UserId,
                ActorMemberId, "Alex Admin", ActionAt, "Access is no longer required."), member.Id, 2),
            DomainEventSeed.Attach(new MemberDeprovisionCleanupCompleted(TenantId, member.Id,
                ActionAt.AddMinutes(1)), member.Id, 3));

        // Act
        var result = scenario.Aggregate.Register("firm_staff");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(scenario.Aggregate.IsRegistered);
        Assert.False(scenario.Aggregate.IsSuspended);
        Assert.False(scenario.Aggregate.IsDeprovisioned);
        Assert.Equal("firm_staff", scenario.Aggregate.Affiliation);
        Assert.NotEqual(priorEpisodeId, scenario.Aggregate.MembershipEpisodeId);
        Assert.IsType<MemberRegistered>(Assert.Single(scenario.PendingEvents));
    }

    [Fact]
    public void ShouldNotReinstateGivenDeprovisionedMember()
    {
        // Arrange
        var member = new Member(TenantId, UserId);
        var scenario = new AggregateScenario<Member>(member).Given(
            DomainEventSeed.Attach(new MemberRegistered(TenantId, member.Id, UserId), member.Id, 1),
            DomainEventSeed.Attach(new MemberDeprovisioned(TenantId, member.Id, UserId,
                ActorMemberId, "Alex Admin", ActionAt, "Access is no longer required."), member.Id, 2));

        // Act
        var result = scenario.Aggregate.Reinstate(ActorMemberId, "Alex Admin", ActionAt.AddHours(1));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public async Task ShouldRetainEveryAttributedLifecycleEventGivenRepeatedSuspensionCycles()
    {
        // Arrange
        var services = new ServiceCollection();
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var member = new Member(TenantId, UserId);
        Assert.True(member.Register().IsSuccess);
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));

        // Act
        member = await reader.HydrateAsync(new Member(TenantId, UserId));
        Assert.True(member.Suspend(ActorMemberId, "Alex Admin", ActionAt,
            "First access review.").IsSuccess);
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        member = await reader.HydrateAsync(new Member(TenantId, UserId));
        Assert.True(member.Reinstate(ActorMemberId, "Alex Admin", ActionAt.AddHours(1)).IsSuccess);
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        member = await reader.HydrateAsync(new Member(TenantId, UserId));
        Assert.True(member.Suspend(ActorMemberId, "Alex Admin", ActionAt.AddHours(2),
            "Second access review.").IsSuccess);
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        var history = new List<DomainEvent>();
        await foreach (var record in events.ReadAsync(new Member(TenantId, UserId).Stream, 0))
            history.Add(record.Event);

        // Assert
        Assert.Collection(history,
            registration => Assert.IsType<MemberRegistered>(registration),
            first =>
            {
                var suspension = Assert.IsType<MemberSuspended>(first);
                Assert.Equal("First access review.", suspension.Reason);
                Assert.Equal(ActorMemberId, suspension.SuspendedByMemberId);
            },
            reinstatement =>
            {
                var restored = Assert.IsType<MemberReinstated>(reinstatement);
                Assert.Equal(ActorMemberId, restored.ReinstatedByMemberId);
            },
            second =>
            {
                var suspension = Assert.IsType<MemberSuspended>(second);
                Assert.Equal("Second access review.", suspension.Reason);
                Assert.Equal(ActionAt.AddHours(2), suspension.SuspendedAt);
            });
        Assert.True((await reader.HydrateAsync(new Member(TenantId, UserId))).IsSuspended);
    }

    [Fact]
    public async Task ShouldPersistOneSuspensionGivenConcurrentMemberWriters()
    {
        // Arrange
        var services = new ServiceCollection();
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var member = new Member(TenantId, UserId);
        Assert.True(member.Register().IsSuccess);
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        var first = await reader.HydrateAsync(new Member(TenantId, UserId));
        var second = await reader.HydrateAsync(new Member(TenantId, UserId));
        Assert.True(first.Suspend(ActorMemberId, "Alex Admin", ActionAt,
            "First concurrent decision.").IsSuccess);
        Assert.True(second.Suspend(ActorMemberId, "Alex Admin", ActionAt,
            "Second concurrent decision.").IsSuccess);

        // Act
        await writer.SaveAsync(first, new RequestDispatchContext(RequestActor.System));
        await Assert.ThrowsAsync<EventStreamConcurrencyException>(async () =>
            await writer.SaveAsync(second, new RequestDispatchContext(RequestActor.System)));
        var suspensionEvents = new List<MemberSuspended>();
        await foreach (var record in events.ReadAsync(new Member(TenantId, UserId).Stream, 0))
        {
            if (record.Event is MemberSuspended suspended)
                suspensionEvents.Add(suspended);
        }

        // Assert
        Assert.Equal("First concurrent decision.", Assert.Single(suspensionEvents).Reason);
        Assert.True((await reader.HydrateAsync(new Member(TenantId, UserId))).IsSuspended);
    }
}
