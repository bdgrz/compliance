using System.Security.Claims;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Fitz.Extensions;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Workforce;

public sealed class FitzWorkRelationshipDirectoryTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Fact]
    public async Task ShouldOrderByWorkerIdAndKeepTenantsSeparateGivenSharedProjection()
    {
        // Arrange
        var directory = new FitzWorkRelationshipDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        await ApplyAsync(directory, tenantId, Recorded(tenantId, "E-200"), Recorded(tenantId, "E-100"));

        // Act
        var page = await directory.ListAsync(tenantId, 50, null);
        var alien = await directory.ListAsync(Uuid.CreateVersion4(), 50, null);

        // Assert
        Assert.Equal(["E-100", "E-200"], page.Items.Select(item => item.SourceWorkerId));
        Assert.All(page.Items, item => Assert.Equal("manual", item.SourceKind));
        Assert.Empty(alien.Items);
    }

    [Fact]
    public async Task ShouldApplyLeaverRevisionAndRejectOutOfOrderGivenRecordedRelationship()
    {
        // Arrange
        var directory = new FitzWorkRelationshipDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var recorded = Recorded(tenantId, "E-100");
        var ended = recorded.Terms with { LifecycleStatus = "ended", EndDate = new DateOnly(2026, 9, 1) };
        await ApplyAsync(directory, tenantId, recorded,
            new WorkRelationshipRevised(tenantId, recorded.RelationshipId, 2, ended, Author, Now));

        // Act
        var view = await directory.GetAsync(tenantId, recorded.RelationshipId);
        var outOfOrder = await Record.ExceptionAsync(() => ApplyAsync(directory, tenantId,
            new WorkRelationshipRevised(tenantId, recorded.RelationshipId, 4, ended, Author, Now)).AsTask());

        // Assert
        Assert.Equal(2, view!.Revision);
        Assert.Equal("ended", view.LifecycleStatus);
        Assert.Equal(new DateOnly(2026, 9, 1), view.EndDate);
        Assert.IsType<InvalidOperationException>(outOfOrder);
    }

    [Fact]
    public async Task ShouldRedactManagerGivenListButNotGivenSingleRead()
    {
        // Arrange
        var directory = new FitzWorkRelationshipDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var recorded = Recorded(tenantId, "E-100");
        await ApplyAsync(directory, tenantId, recorded);
        var handler = new ListWorkRelationshipsHandler(directory,
            new WorkRelationshipReadConsistency(directory, new NoSource(), new InMemoryEventStore()));
        var context = new RequestContext<ListWorkRelationships>(new ListWorkRelationships(tenantId), Actor());

        // Act
        var listed = await handler.HandleAsync(context, CancellationToken.None);
        var single = await directory.GetAsync(tenantId, recorded.RelationshipId);

        // Assert
        var item = Assert.Single(listed.Value.Items);
        Assert.Null(item.ManagerPersonId);
        Assert.True(item.RestrictedFieldsRedacted);
        Assert.Equal(recorded.Terms.ManagerPersonId, single!.ManagerPersonId);
        Assert.False(single.RestrictedFieldsRedacted);
    }

    static WorkRelationshipRecorded Recorded(Uuid tenantId, string workerId) =>
        new(tenantId, WorkRelationship.IdFor(tenantId, workerId), Uuid.CreateVersion4(), workerId,
            new WorkRelationshipTerms("employee", "active", new DateOnly(2025, 1, 6), null,
                "Engineering", Uuid.CreateVersion4(), null), Author, Now);

    static async ValueTask ApplyAsync(FitzWorkRelationshipDirectory directory, Uuid tenantId,
        params DomainEvent[] events)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("WorkRelationshipDirectoryV1",
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

    static ClaimsPrincipal Actor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));
}
