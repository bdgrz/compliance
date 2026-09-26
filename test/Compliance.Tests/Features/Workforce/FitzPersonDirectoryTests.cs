using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Workforce;

public sealed class FitzPersonDirectoryTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldPageByNameAndKeepTenantsSeparateGivenSharedProjection()
    {
        // Arrange
        var directory = new FitzPersonDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            foreach (var index in Enumerable.Range(1, 201))
                await directory.ApplyAsync(Recorded(tenantId, $"Person {index:D3}"));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var first = await directory.ListAsync(tenantId, 200, null);
        var second = await directory.ListAsync(tenantId, 200, first.NextCursor);
        var alien = await directory.ListAsync(otherTenantId, 200, null);

        // Assert
        Assert.Equal(200, first.Items.Count);
        Assert.Equal("Person 001", first.Items[0].DisplayName);
        Assert.Equal("Person 201", Assert.Single(second.Items).DisplayName);
        Assert.Empty(alien.Items);
        Assert.All(first.Items, item => Assert.Equal("manual", item.SourceKind));
    }

    [Fact]
    public async Task ShouldApplyRevisionAndRejectOutOfOrderRevisionGivenRecordedPerson()
    {
        // Arrange
        var directory = new FitzPersonDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var recorded = Recorded(tenantId, "Ada Lovelace");
        var editor = ActorReference.ForMember(Uuid.CreateVersion4(), "Editor");
        PersonRevised revised = new(tenantId, recorded.PersonId, 2, "Ada King",
            "ada@example.com", editor, Now.AddMinutes(1));
        var skipped = revised with { Revision = 4 };

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(recorded);
            await directory.ApplyAsync(revised);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(skipped));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var current = await directory.GetAsync(tenantId, recorded.PersonId);
        var alien = await directory.GetAsync(Uuid.CreateVersion4(), recorded.PersonId);

        // Assert
        Assert.NotNull(current);
        Assert.Equal(2, current.Revision);
        Assert.Equal("Ada King", current.DisplayName);
        Assert.Equal("ada@example.com", current.WorkEmail);
        Assert.Equal(editor, current.LastChangedBy);
        Assert.Null(alien);
    }

    static PersonRecorded Recorded(Uuid tenantId, string name) => new(tenantId,
        Uuid.CreateVersion4(), name, null,
        ActorReference.ForMember(Uuid.CreateVersion4(), "Author"), Now);

    static CheckpointIdentity Identity(Uuid tenantId) => new("PersonDirectoryV1",
        EventStreamPattern.ForPattern(tenantId.ToString(), "people"));
}
