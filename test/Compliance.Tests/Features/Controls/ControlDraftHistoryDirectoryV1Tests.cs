using System.Security.Claims;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz.Extensions;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class ControlDraftHistoryDirectoryV1Tests
{
    [Fact]
    public async Task ShouldPreserveImmutablePagedRevisionsGivenReplayedDraftEvents()
    {
        // Arrange
        var directory = new FitzControlDraftHistoryDirectoryV1(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = ControlDraft.IdFor(tenantId, programId, "AC-01");
        var now = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        var created = Created(tenantId, programId, controlId, "AC-01", Content("Original"), now);
        var second = Revised(created, 2, Content("Second"), now.AddMinutes(1));
        var third = Revised(created, 3, Content("Third"), now.AddMinutes(2));
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(Checkpoint(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(created);
            await directory.ApplyAsync(second);
            await directory.ApplyAsync(third);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var first = await directory.ListRevisionsAsync(tenantId, controlId, 2, null);
        var secondPage = await directory.ListRevisionsAsync(tenantId, controlId, 2,
            first.NextCursor);
        var foreign = await directory.ListRevisionsAsync(otherTenantId, controlId, 2, null);

        // Assert
        Assert.Equal([1L, 2L], first.Items.Select(item => item.Revision));
        Assert.Equal([3L], secondPage.Items.Select(item => item.Revision));
        Assert.Equal("Original", first.Items[0].Content.Title);
        Assert.Equal("Second", first.Items[1].Content.Title);
        Assert.Equal("Third", Assert.Single(secondPage.Items).Content.Title);
        Assert.Equal("AC-01", first.Items[1].Identifier);
        Assert.Equal("AC-01", Assert.Single(secondPage.Items).Identifier);
        Assert.Empty(foreign.Items);
        Assert.Null(await directory.GetRevisionAsync(otherTenantId, controlId, 1));
    }

    [Fact]
    public async Task ShouldRejectMalformedAndCrossControlCursorsGivenRevisionHistoryPage()
    {
        // Arrange
        var directory = new FitzControlDraftHistoryDirectoryV1(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = ControlDraft.IdFor(tenantId, programId, "AC-01");
        var otherControlId = ControlDraft.IdFor(tenantId, programId, "AC-02");
        var now = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        var other = Created(tenantId, programId, otherControlId, "AC-02", Content("Other"), now);
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(Checkpoint(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(Created(tenantId, programId, controlId, "AC-01",
                Content("Target"), now));
            await directory.ApplyAsync(other);
            await directory.ApplyAsync(Revised(other, 2, Content("Other revised"),
                now.AddMinutes(1)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var foreignCursor = (await directory.ListRevisionsAsync(tenantId, otherControlId, 1,
            null)).NextCursor;
        var source = new ControlDraft(tenantId, controlId);
        Assert.True(source.Create(programId, Uuid.CreateVersion4(), "AC-01", Content("Target"),
            Uuid.CreateVersion4(), "Author", now).IsSuccess);
        var handler = new ListControlDraftRevisionsHandler(directory,
            new ControlDraftHistoryReadConsistency(directory, new SourceReader(source)));

        // Act
        var crossDirectory = Assert.ThrowsAsync<KvDirectoryQueryException>(async () =>
            await directory.ListRevisionsAsync(tenantId, controlId, 1, foreignCursor));
        var malformedDirectory = Assert.ThrowsAsync<KvDirectoryQueryException>(async () =>
            await directory.ListRevisionsAsync(tenantId, controlId, 1, "not-a-cursor"));
        var crossControl = await handler.HandleAsync(Context(new ListControlDraftRevisions(tenantId,
            programId, controlId, Cursor: foreignCursor)), CancellationToken.None);
        var malformed = await handler.HandleAsync(Context(new ListControlDraftRevisions(tenantId,
            programId, controlId, Cursor: "not-a-cursor")), CancellationToken.None);

        // Assert
        Assert.NotNull(foreignCursor);
        await crossDirectory;
        await malformedDirectory;
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(crossControl.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(malformed.Error).Kind);
    }

    [Fact]
    public async Task ShouldRollBackAndReplayHistoryGivenOutOfOrderRevision()
    {
        // Arrange
        var directory = new FitzControlDraftHistoryDirectoryV1(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = ControlDraft.IdFor(tenantId, programId, "AC-01");
        var now = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        var created = Created(tenantId, programId, controlId, "AC-01", Content("Original"), now);
        var invalid = Revised(created, 3, Content("Skipped"), now.AddMinutes(1));
        var identity = Checkpoint(tenantId);

        // Act
        await using (var failed = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(created);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(invalid));
        }
        await using (var replay = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(created);
            await directory.ApplyAsync(Revised(created, 2, Content("Second"),
                now.AddMinutes(1)));
            await replay.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        var page = await directory.ListRevisionsAsync(tenantId, controlId, 10, null);
        Assert.Equal([1L, 2L], page.Items.Select(item => item.Revision));
        Assert.Equal(ProjectionCheckpoint.Start, await directory.LoadCheckpointAsync(tenantId));
    }

    static CheckpointIdentity Checkpoint(Uuid tenantId) => new(
        "ControlDraftHistoryDirectoryV1", EventStreamPattern.ForPattern(tenantId.ToString(),
            "controls"));

    static ControlDraftCreated Created(Uuid tenantId, Uuid programId, Uuid controlId,
        string identifier, ControlDraftContent content, DateTimeOffset changedAt) => new(tenantId,
        programId, controlId, Uuid.CreateVersion4(), identifier, content, Uuid.CreateVersion4(),
        "Author", changedAt);

    static ControlDraftRevised Revised(ControlDraftCreated created, long revision,
        ControlDraftContent content, DateTimeOffset changedAt) => new(created.TenantId,
        created.ProgramId, created.ControlId, revision, content, Uuid.CreateVersion4(), "Reviewer",
        changedAt);

    static ControlDraftContent Content(string title) => new(title, "Review access",
        "Management reviews access.", "The owner reviews access quarterly.", ["Review record"]);

    static RequestContext<TRequest> Context<TRequest>(TRequest request)
        where TRequest : IRequestBase => new(request, new ClaimsPrincipal());

    sealed class SourceReader(Aggregate source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)source);
    }
}
