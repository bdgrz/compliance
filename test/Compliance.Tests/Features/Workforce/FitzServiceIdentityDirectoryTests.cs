using System.Security.Claims;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Workforce;

public sealed class FitzServiceIdentityDirectoryTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = new(2026, 9, 30);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Fact]
    public async Task ShouldListOnlyUnownedWorkGivenExpiredReviewAndEndedOwner()
    {
        // Arrange
        var kv = new InMemoryKvClient();
        var identities = new FitzServiceIdentityDirectory(kv);
        var roster = new FitzWorkforceObservationDirectory(kv);
        var tenantId = Uuid.CreateVersion4();
        var active = Uuid.CreateVersion4();
        var leaver = Uuid.CreateVersion4();
        await ApplyRosterAsync(roster, tenantId, Relationship(tenantId, active, "E-1", "active"),
            Relationship(tenantId, leaver, "E-2", "ended"));
        var owned = Recorded(tenantId, "a-owned", active, Today.AddMonths(3));
        var expired = Recorded(tenantId, "b-expired", active, Today.AddMonths(3));
        var orphaned = Recorded(tenantId, "c-orphaned", leaver, Today.AddMonths(3));
        await ApplyAsync(identities, tenantId, owned, expired, orphaned);
        var handler = Handler(identities, roster, new FixedClock(Now.AddMonths(4)));
        var laterHandler = Handler(identities, roster, new FixedClock(Now));

        // Act
        var afterReview = await handler.HandleAsync(Context(tenantId, true), CancellationToken.None);
        var beforeReview = await laterHandler.HandleAsync(Context(tenantId, null),
            CancellationToken.None);
        var alien = await laterHandler.HandleAsync(Context(Uuid.CreateVersion4(), null),
            CancellationToken.None);

        // Assert
        Assert.Equal(["a-owned", "b-expired", "c-orphaned"],
            afterReview.Value.Items.Select(item => item.DisplayName));
        Assert.Equal(["owner_relationship_ended", "review_expired"],
            afterReview.Value.Items[2].UnownedReasons);
        Assert.Equal([false, false, true], beforeReview.Value.Items.Select(item => item.Unowned));
        Assert.Empty(alien.Value.Items);
    }

    static ListServiceIdentitiesHandler Handler(FitzServiceIdentityDirectory identities,
        FitzWorkforceObservationDirectory roster, TimeProvider clock)
    {
        var events = new InMemoryEventStore();
        return new ListServiceIdentitiesHandler(identities,
            new ServiceIdentityReadConsistency(identities, new NoSource(), events),
            new ServiceIdentityOwnership(roster, new NoSource(),
                new WorkforceObservationReadConsistency(roster, events), clock));
    }

    static RequestContext<ListServiceIdentities> Context(Uuid tenantId, bool? unownedOnly) =>
        new(new ListServiceIdentities(tenantId, unownedOnly), Actor());

    static ServiceIdentityRecorded Recorded(Uuid tenantId, string name, Uuid ownerId,
        DateOnly reviewBy) =>
        new(tenantId, Uuid.CreateVersion4(), new ServiceIdentityTerms(name, "workload",
            "Nightly export", null, "active", "person", ownerId, reviewBy), Author, Now);

    static WorkRelationshipRecorded Relationship(Uuid tenantId, Uuid personId, string workerId,
        string status) =>
        new(tenantId, WorkRelationship.IdFor(tenantId, workerId), personId, workerId,
            new WorkRelationshipTerms("employee", status, new DateOnly(2025, 1, 6),
                status == "ended" ? new DateOnly(2026, 9, 1) : null, null, null, null), Author, Now);

    static async ValueTask ApplyAsync(FitzServiceIdentityDirectory directory, Uuid tenantId,
        params DomainEvent[] events)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("ServiceIdentityDirectoryV1",
                EventStreamPattern.ForPattern(tenantId.ToString(), "service-identities")),
            ProjectionCheckpoint.Start));
        foreach (var domainEvent in events)
            await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    static async ValueTask ApplyRosterAsync(FitzWorkforceObservationDirectory directory,
        Uuid tenantId, params DomainEvent[] events)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("WorkforceObservationsV1",
                EventStreamPattern.ForPattern(tenantId.ToString(), "work-relationships")),
            ProjectionCheckpoint.Start));
        foreach (var domainEvent in events)
            await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    sealed class NoSource : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate => ValueTask.FromResult(aggregate);
    }

    sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    static ClaimsPrincipal Actor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));
}
