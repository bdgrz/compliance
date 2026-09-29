using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class FitzTeamMemberDirectoryReaderTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid TeamId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldReturnEveryMemberGivenTeamQuery()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        await SeedAsync(client, TeamId, first);
        await SeedAsync(client, TeamId, second);
        var reader = new FitzTeamMemberDirectoryReader(client, new InMemoryEventStore());

        // Act
        var page = await reader.ListAsync(
            TenantId, TeamId, null, null, null, descending: false, CancellationToken.None);

        // Assert
        Assert.Equal(2, page.Items.Count);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task ShouldFindMemberRemovalAfterCheckpointGivenLaggingTeamProjection()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var events = new InMemoryEventStore();
        var memberId = Uuid.CreateVersion4();
        var reader = new FitzTeamMemberDirectoryReader(client, events);
        var pattern = EventStreamPattern.ForPattern(TenantId.ToString());
        var identity = new CheckpointIdentity("TeamMemberDirectory", pattern);
        await using (var batch = await reader.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
        {
            await reader.ApplyAsync(new TeamMemberAssigned(TenantId, TeamId, memberId));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var assignmentId = RbacIds.TeamMember(TenantId, TeamId, memberId);
        DomainEvent removed = new TeamMemberRemoved(TenantId, TeamId, memberId);
        removed.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), assignmentId, 1,
            DateTimeOffset.UtcNow));
        await events.AppendAsync(new EventStreamAddress(TenantId.ToString(), "rbac-team-members",
            assignmentId.ToString()), 0, [removed]);

        // Act
        var pendingRemoval = await reader.HasPendingRemovalAsync(TenantId, TeamId, memberId);

        // Assert
        Assert.True(pendingRemoval);
    }

    [Fact]
    public async Task ShouldHonorLaterAssignmentGivenRemovalAndReassignmentBeforeProjectionCatchesUp()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var events = new InMemoryEventStore();
        var memberId = Uuid.CreateVersion4();
        var reader = new FitzTeamMemberDirectoryReader(client, events);
        var pattern = EventStreamPattern.ForPattern(TenantId.ToString());
        var identity = new CheckpointIdentity("TeamMemberDirectory", pattern);
        await using (var batch = await reader.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
        {
            await reader.ApplyAsync(new TeamMemberAssigned(TenantId, TeamId, memberId));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var assignmentId = RbacIds.TeamMember(TenantId, TeamId, memberId);
        var address = new EventStreamAddress(TenantId.ToString(), "rbac-team-members",
            assignmentId.ToString());
        DomainEvent removed = new TeamMemberRemoved(TenantId, TeamId, memberId);
        removed.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), assignmentId, 1,
            DateTimeOffset.UtcNow));
        DomainEvent reassigned = new TeamMemberAssigned(TenantId, TeamId, memberId);
        reassigned.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), assignmentId, 2,
            DateTimeOffset.UtcNow.AddSeconds(1)));
        await events.AppendAsync(address, 0, [removed, reassigned]);

        // Act
        var pendingRemoval = await reader.HasPendingRemovalAsync(TenantId, TeamId, memberId);

        // Assert
        Assert.False(pendingRemoval);
    }

    [Fact]
    public async Task ShouldReturnMembersOnlyGivenRequestedTeam()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var otherTeamId = Uuid.CreateVersion4();
        var member = Uuid.CreateVersion4();
        var otherMember = Uuid.CreateVersion4();
        await SeedAsync(client, TeamId, member);
        await SeedAsync(client, otherTeamId, otherMember);
        var reader = new FitzTeamMemberDirectoryReader(client, new InMemoryEventStore());

        // Act
        var page = await reader.ListAsync(
            TenantId, TeamId, null, null, null, descending: false, CancellationToken.None);

        // Assert
        var result = Assert.Single(page.Items);
        Assert.Equal(member, result.MemberId);
    }

    [Fact]
    public async Task ShouldAvoidEmptyPageGivenReturnedCursor()
    {
        // Arrange
        var client = new InMemoryKvClient();
        await SeedAsync(client, TeamId, Uuid.CreateVersion4());
        await SeedAsync(client, TeamId, Uuid.CreateVersion4());
        var reader = new FitzTeamMemberDirectoryReader(client, new InMemoryEventStore());

        // Act
        var firstPage = await reader.ListAsync(
            TenantId, TeamId, 1, null, null, descending: false, CancellationToken.None);

        // Assert
        Assert.Single(firstPage.Items);
        Assert.NotNull(firstPage.NextCursor);

        var secondPage = await reader.ListAsync(
            TenantId, TeamId, 1, firstPage.NextCursor, null, descending: false, CancellationToken.None);

        Assert.Single(secondPage.Items);
        Assert.Null(secondPage.NextCursor);
    }

    // Regression test: Search must match a substring anywhere in the member ID, not just a prefix —
    // the KvDirectory 1.3.0 migration briefly narrowed this to prefix-only before being caught and
    // fixed.
    [Fact]
    public async Task ShouldFilterMembersGivenIdSubstring()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var member = Uuid.CreateVersion4();
        var otherMember = Uuid.CreateVersion4();
        await SeedAsync(client, TeamId, member);
        await SeedAsync(client, TeamId, otherMember);
        var reader = new FitzTeamMemberDirectoryReader(client, new InMemoryEventStore());
        var middleOfMemberId = member.ToString().Substring(9, 8);

        // Act
        var page = await reader.ListAsync(
            TenantId, TeamId, null, null, middleOfMemberId, descending: false, CancellationToken.None);

        // Assert
        var result = Assert.Single(page.Items);
        Assert.Equal(member, result.MemberId);
    }

    static async Task SeedAsync(InMemoryKvClient client, Uuid teamId, Uuid memberId)
    {
        var repository = new FitzTeamMemberDirectoryReader(client, new InMemoryEventStore());
        var identity = new CheckpointIdentity("TeamMemberDirectory", EventStreamPattern.ForPattern(TenantId.ToString()));
        await using var batch = await repository.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        await repository.ApplyAsync(new TeamMemberAssigned(TenantId, teamId, memberId));
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
