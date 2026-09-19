using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class FitzEmailAddressDirectoryTests
{
    [Fact]
    public async Task ListsOnlyAnOwnersAddressesAndTracksVerification()
    {
        var client = new InMemoryKvClient();
        var directory = new FitzEmailAddressDirectory(client);
        var owner = Uuid.CreateVersion4();
        var other = Uuid.CreateVersion4();
        var identity = new CheckpointIdentity("EmailAddressDirectory", EventStreamPattern.ForPattern("bdgrz"));
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new EmailAddressReserved(owner, "first@example.com"));
            await directory.ApplyAsync(new EmailAddressReserved(owner, "second@example.com"));
            await directory.ApplyAsync(new EmailAddressReserved(other, "other@example.com"));
            await directory.ApplyAsync(new EmailAddressVerified(owner, "first@example.com", Uuid.CreateVersion4()));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        var first = await directory.GetAsync("first@example.com");
        var page = await directory.ListAsync(owner, null, null);

        Assert.True(first?.Verified);
        Assert.Equal(2, page.Items.Count);
        Assert.DoesNotContain(page.Items, address => address.UserId == other);
        Assert.Contains(page.Items, address => address.EmailAddress == "second@example.com" && !address.Verified);
    }
}
