using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class FitzPlatformUserDirectoryTests
{
    [Fact]
    public async Task ShouldUpdateAndClearCurrentNameGivenRetainedProfileObservations()
    {
        // Arrange
        var directory = new FitzPlatformUserDirectory(new InMemoryKvClient());
        var userId = Uuid.CreateVersion4();
        var otherUser = Uuid.CreateVersion4();
        var identity = new CheckpointIdentity("PlatformUserDirectoryV2",
            EventStreamPattern.ForPattern("bdgrz", "user-identities"));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new UserIdentityRegistered(userId, "issuer", "subject", null));
            await directory.ApplyAsync(new UserIdentityProfileObserved(userId, "Ada", DateTimeOffset.UtcNow));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        Assert.Equal("Ada", await directory.ReadAsync(userId));
        Assert.Null(await directory.ReadAsync(otherUser));
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new UserIdentityProfileObserved(userId, "Ada King", DateTimeOffset.UtcNow));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        Assert.Equal("Ada King", await directory.ReadAsync(userId));
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new UserIdentityProfileObserved(userId, null, DateTimeOffset.UtcNow));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        Assert.Null(await directory.ReadAsync(userId));
        Assert.True(await directory.ExistsAsync(userId));
    }

    [Fact]
    public async Task ShouldFindOnePlatformUserGivenTwoLinkedProviderIdentities()
    {
        // Arrange
        var directory = new FitzPlatformUserDirectory(new InMemoryKvClient());
        var userId = Uuid.CreateVersion4();
        var identity = new CheckpointIdentity("PlatformUserDirectoryV2",
            EventStreamPattern.ForPattern("bdgrz", "user-identities"));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new UserIdentityRegistered(userId,
                "issuer-a", "subject-a", null));
            await directory.ApplyAsync(new UserIdentityRegistered(userId,
                "issuer-b", "subject-b", null));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        Assert.True(await directory.ExistsAsync(userId));
        Assert.False(await directory.ExistsAsync(Uuid.CreateVersion4()));
    }
}
