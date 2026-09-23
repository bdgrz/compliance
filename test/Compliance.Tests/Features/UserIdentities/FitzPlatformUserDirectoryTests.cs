using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class FitzPlatformUserDirectoryTests
{
    [Fact]
    public async Task ShouldFindOnePlatformUserGivenTwoLinkedProviderIdentities()
    {
        // Arrange
        var directory = new FitzPlatformUserDirectory(new InMemoryKvClient());
        var userId = Uuid.CreateVersion4();
        var identity = new CheckpointIdentity("PlatformUserDirectory",
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
