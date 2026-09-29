using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class FitzAccessGrantDirectoryTests
{
    static readonly DateTimeOffset EffectiveFrom = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldPersistGrantHistoryAndOneRevisionPerFactGivenReplay()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var directory = new FitzAccessGrantDirectory(client, new EmptyEventReader());
        var tenantId = Uuid.CreateVersion4();
        var grantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var terms = new AccessGrantTerms(
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, memberId),
            Uuid.CreateVersion4(),
            new AccessGrantScope(AccessGrantScopeKind.Program, Uuid.CreateVersion4()),
            new AccessGrantSource("manual", "request-123"),
            ActorReference.ForMember(memberId, "Organization Admin"),
            EffectiveFrom,
            null);
        var issued = new AccessGrantIssued(tenantId, grantId, terms);
        var revoked = new AccessGrantRevoked(tenantId, grantId,
            ActorReference.ForMember(memberId, "Organization Admin"), EffectiveFrom.AddMinutes(1));
        var identity = new CheckpointIdentity(AccessGrantProjectionKeys.Projector,
            EventStreamPattern.ForPattern(tenantId.ToString()));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(issued);
            await directory.ApplyAsync(issued);
            await directory.ApplyAsync(revoked);
            await directory.ApplyAsync(revoked);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var view = await directory.ListAsync(tenantId);

        // Assert
        Assert.Equal(2, view.Revision);
        var grant = Assert.Single(view.Grants);
        Assert.Equal(terms, grant.Terms);
        Assert.Equal(ActorReference.ForMember(memberId, "Organization Admin"), grant.RevokedBy);
        Assert.Equal(revoked.RevokedAt, grant.RevokedAt);
        Assert.Null(await directory.GetAsync(tenantId, Uuid.CreateVersion4()));
    }

    [Fact]
    public async Task ShouldSeparateGrantsByTenantGivenIdenticalGrantId()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var directory = new FitzAccessGrantDirectory(client, new EmptyEventReader());
        var tenantA = Uuid.CreateVersion4();
        var tenantB = Uuid.CreateVersion4();
        var grantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var terms = new AccessGrantTerms(
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, memberId),
            Uuid.CreateVersion4(),
            new AccessGrantScope(AccessGrantScopeKind.Organization, tenantA),
            new AccessGrantSource("manual", "request-123"),
            ActorReference.ForMember(memberId, "Organization Admin"),
            EffectiveFrom,
            null);
        var identity = new CheckpointIdentity(AccessGrantProjectionKeys.Projector,
            EventStreamPattern.ForPattern(tenantA.ToString()));
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new AccessGrantIssued(tenantA, grantId, terms));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var sameTenant = await directory.GetAsync(tenantA, grantId);
        var otherTenant = await directory.GetAsync(tenantB, grantId);
        var emptyTenant = await directory.ListAsync(tenantB);

        // Assert
        Assert.NotNull(sameTenant);
        Assert.Equal(tenantA, sameTenant.TenantId);
        Assert.Null(otherTenant);
        Assert.Equal(0, emptyTenant.Revision);
        Assert.Empty(emptyTenant.Grants);
    }

    [Fact]
    public async Task ShouldFindCommittedRevocationAfterProjectionCheckpointGivenLaggingProjector()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var events = new InMemoryEventStore();
        var tenantId = Uuid.CreateVersion4();
        var grantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString());
        var identity = new CheckpointIdentity(AccessGrantProjectionKeys.Projector, pattern);
        var directory = new FitzAccessGrantDirectory(client, events);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
            await batch.CommitAsync(ProjectionCheckpoint.Start);

        DomainEvent revoked = new AccessGrantRevoked(tenantId, grantId,
            ActorReference.ForMember(memberId, "Organization Admin"), EffectiveFrom);
        revoked.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), grantId, 1,
            EffectiveFrom));
        await events.AppendAsync(new EventStreamAddress(tenantId.ToString(), "access-grants",
            grantId.ToString()), 0, [revoked]);

        // Act
        var pending = await directory.FindPendingRevocationsAsync(tenantId, new HashSet<Uuid> { grantId });

        // Assert
        Assert.Contains(grantId, pending);
    }

    sealed class EmptyEventReader : IDomainEventReader
    {
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream,
            ulong fromVersion, [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern,
            EventCursor after, [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
