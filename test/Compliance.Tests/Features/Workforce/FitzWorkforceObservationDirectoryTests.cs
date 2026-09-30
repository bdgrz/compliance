using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Workforce;

public sealed class FitzWorkforceObservationDirectoryTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");
    static readonly DateOnly Start = new(2025, 1, 6);

    [Fact]
    public async Task ShouldObserveJoinerMoverAndLeaverGivenRosterRevisions()
    {
        // Arrange
        var directory = new FitzWorkforceObservationDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var recorded = Recorded(tenantId, "E-100");
        var moved = recorded.Terms with { Department = "Finance" };
        var managerOnly = moved with { ManagerPersonId = Uuid.CreateVersion4() };
        var ended = managerOnly with { LifecycleStatus = "ended", EndDate = new DateOnly(2026, 9, 1) };
        var rehired = ended with { LifecycleStatus = "active", EndDate = null };

        // Act
        await ApplyAsync(directory, tenantId, recorded,
            Revised(recorded, 2, moved, 1), Revised(recorded, 3, managerOnly, 2),
            Revised(recorded, 4, ended, 3), Revised(recorded, 5, rehired, 4));
        var all = await directory.ListAsync(tenantId, null, 50, null);
        var leavers = await directory.ListAsync(tenantId, "leaver", 50, null);

        // Assert
        Assert.Equal(["joiner", "mover", "leaver", "joiner"], all.Items.Select(item => item.Kind));
        Assert.Equal([1L, 2L, 4L, 5L], all.Items.Select(item => item.RelationshipRevision));
        Assert.Equal(["department"], all.Items[1].ChangedFields);
        Assert.All(all.Items, item => Assert.Equal("open", item.Status));
        var leaver = Assert.Single(leavers.Items);
        Assert.Equal(new DateOnly(2026, 9, 1), leaver.EffectiveDate);
        Assert.Equal(recorded.PersonId, leaver.PersonId);
    }

    [Fact]
    public async Task ShouldReportOwnerStatusesAndKeepTenantsSeparateGivenSharedProjection()
    {
        // Arrange
        var directory = new FitzWorkforceObservationDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var recorded = Recorded(tenantId, "E-100");
        var ended = recorded.Terms with { LifecycleStatus = "ended", EndDate = new DateOnly(2026, 9, 1) };
        await ApplyAsync(directory, tenantId, recorded, Revised(recorded, 2, ended, 1));

        // Act
        var statuses = await directory.ListRelationshipStatusesAsync(tenantId, recorded.PersonId);
        var alien = await directory.ListAsync(Uuid.CreateVersion4(), null, 50, null);
        var alienStatuses = await directory.ListRelationshipStatusesAsync(Uuid.CreateVersion4(),
            recorded.PersonId);

        // Assert
        Assert.Equal(["ended"], statuses);
        Assert.Empty(alien.Items);
        Assert.Empty(alienStatuses);
    }

    static WorkRelationshipRecorded Recorded(Uuid tenantId, string workerId) =>
        new(tenantId, WorkRelationship.IdFor(tenantId, workerId), Uuid.CreateVersion4(), workerId,
            new WorkRelationshipTerms("employee", "active", Start, null, "Engineering",
                Uuid.CreateVersion4(), null), Author, Now);

    static WorkRelationshipRevised Revised(WorkRelationshipRecorded recorded, long revision,
        WorkRelationshipTerms terms, int minutes) =>
        new(recorded.TenantId, recorded.RelationshipId, revision, terms, Author,
            Now.AddMinutes(minutes));

    static async ValueTask ApplyAsync(FitzWorkforceObservationDirectory directory, Uuid tenantId,
        params DomainEvent[] events)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("WorkforceObservationsV1",
                EventStreamPattern.ForPattern(tenantId.ToString(), "work-relationships")),
            ProjectionCheckpoint.Start));
        foreach (var domainEvent in events)
            await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
