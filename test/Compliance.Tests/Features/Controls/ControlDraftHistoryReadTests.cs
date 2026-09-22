using System.Security.Claims;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class ControlDraftHistoryReadTests
{
    [Fact]
    public async Task ShouldReturnTransientConflictGivenHistoryProjectionLag()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = ControlDraft.IdFor(tenantId, programId, "AC-01");
        var source = Source(tenantId, programId, controlId);
        var history = new HistoryDirectory();
        var handler = new ListControlDraftRevisionsHandler(history,
            new ControlDraftHistoryReadConsistency(history, new SourceReader(source)));

        // Act
        var result = await handler.HandleAsync(Context(new ListControlDraftRevisions(tenantId,
            programId, controlId)), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
        Assert.Contains("history projection", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenRequestedRevisionAheadOfSource()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = ControlDraft.IdFor(tenantId, programId, "AC-01");
        var history = new HistoryDirectory();
        var handler = new ListControlDraftRevisionsHandler(history,
            new ControlDraftHistoryReadConsistency(history,
                new SourceReader(Source(tenantId, programId, controlId))));

        // Act
        var result = await handler.HandleAsync(Context(new ListControlDraftRevisions(tenantId,
            programId, controlId, MinimumControlDraftRevision: 2)), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
        Assert.Contains("source", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, history.ReadCalls);
    }

    [Fact]
    public async Task ShouldHideForeignProgramBeforeReadingHistoryGivenRevisionPage()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = ControlDraft.IdFor(tenantId, programId, "AC-01");
        var history = new HistoryDirectory();
        var handler = new ListControlDraftRevisionsHandler(history,
            new ControlDraftHistoryReadConsistency(history,
                new SourceReader(Source(tenantId, programId, controlId))));

        // Act
        var result = await handler.HandleAsync(Context(new ListControlDraftRevisions(tenantId,
            Uuid.CreateVersion4(), controlId)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(0, history.ReadCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task ShouldRejectOutOfRangeLimitGivenRevisionHistoryPage(int limit)
    {
        // Arrange
        var history = new HistoryDirectory();
        var handler = new ListControlDraftRevisionsHandler(history,
            new ControlDraftHistoryReadConsistency(history,
                new SourceReader(new ControlDraft(Uuid.CreateVersion4(), Uuid.CreateVersion4()))));

        // Act
        var result = await handler.HandleAsync(Context(new ListControlDraftRevisions(
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), limit)),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldReturnValidationGivenMalformedHistoryCursor()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = ControlDraft.IdFor(tenantId, programId, "AC-01");
        var source = Source(tenantId, programId, controlId);
        var history = new HistoryDirectory
        {
            Latest = Revision(tenantId, programId, controlId, 1),
            RejectCursor = true,
        };
        var handler = new ListControlDraftRevisionsHandler(history,
            new ControlDraftHistoryReadConsistency(history, new SourceReader(source)));

        // Act
        var result = await handler.HandleAsync(Context(new ListControlDraftRevisions(tenantId,
            programId, controlId, Cursor: "not-a-cursor")), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
    }

    static RequestContext<TRequest> Context<TRequest>(TRequest request)
        where TRequest : IRequestBase => new(request, new ClaimsPrincipal());

    static ControlDraft Source(Uuid tenantId, Uuid programId, Uuid controlId)
    {
        var source = new ControlDraft(tenantId, controlId);
        Assert.True(source.Create(programId, Uuid.CreateVersion4(), "AC-01", Content("Original"),
            Uuid.CreateVersion4(), "Author", DateTimeOffset.UtcNow).IsSuccess);
        return source;
    }

    static ControlDraftRevisionView Revision(Uuid tenantId, Uuid programId, Uuid controlId,
        long revision) => new(tenantId, programId, controlId, "AC-01", revision,
        Content("Original"), Uuid.CreateVersion4(), "Author", DateTimeOffset.UtcNow);

    static ControlDraftContent Content(string title) => new(title, "Review access",
        "Management reviews access.", "The owner reviews access quarterly.", ["Review record"]);

    sealed class SourceReader(Aggregate source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)source);
    }

    sealed class HistoryDirectory : IControlDraftHistoryDirectoryReader
    {
        public int ReadCalls { get; private set; }
        public bool RejectCursor { get; init; }
        public ControlDraftRevisionView? Latest { get; init; }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);

        public ValueTask<ControlDraftRevisionView?> GetRevisionAsync(Uuid tenantId,
            Uuid controlId, long revision, CancellationToken ct = default)
        {
            ReadCalls++;
            return ValueTask.FromResult(Latest);
        }

        public ValueTask<Page<ControlDraftRevisionView>> ListRevisionsAsync(Uuid tenantId,
            Uuid controlId, int limit, string? cursor, CancellationToken ct = default)
        {
            ReadCalls++;
            return RejectCursor
                ? ValueTask.FromException<Page<ControlDraftRevisionView>>(
                    new KvDirectoryQueryException())
                : ValueTask.FromResult(new Page<ControlDraftRevisionView>([], null));
        }
    }
}
