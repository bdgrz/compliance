using System.Security.Claims;
using Bdgrz.Compliance.Features.Risks;
using Cntryl.Fitz.Extensions;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class FitzRiskDraftHistoryDirectoryV1Tests
{
    [Fact]
    public async Task ShouldPreserveImmutableRowsAndPagePerDraftGivenReplayedRiskEvents()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var riskId = RiskDraft.IdFor(tenantId, programId, "R-01");
        var otherRiskId = RiskDraft.IdFor(tenantId, programId, "R-02");
        var authorId = Uuid.CreateVersion4();
        var editorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        var created = new RiskDraftCreated(tenantId, programId, riskId,
            Uuid.CreateVersion4(), "R-01", Content("Initial"), authorId, "Author", now);
        var second = new RiskDraftRevised(tenantId, programId, riskId, 2,
            Content("Second"), editorId, "Editor", now.AddMinutes(1));
        var third = new RiskDraftRevised(tenantId, programId, riskId, 3,
            Content("Third"), editorId, "Editor", now.AddMinutes(2));
        var other = new RiskDraftCreated(tenantId, programId, otherRiskId,
            Uuid.CreateVersion4(), "R-02", Content("Other"), authorId, "Author", now);
        var directory = new FitzRiskDraftHistoryDirectoryV1(new InMemoryKvClient());
        await ProjectAsync(directory, tenantId, created, second, third, other);

        // Act
        var first = await directory.ListRevisionsAsync(tenantId, riskId, 2, null);
        var secondPage = await directory.ListRevisionsAsync(tenantId, riskId, 2,
            first.NextCursor);
        var otherPage = await directory.ListRevisionsAsync(tenantId, otherRiskId, 2, null);

        // Assert
        Assert.Equal([1L, 2L], first.Items.Select(item => item.Revision));
        Assert.Equal([3L], secondPage.Items.Select(item => item.Revision));
        Assert.Equal("Initial", first.Items[0].Content.Title);
        Assert.Equal(authorId, first.Items[0].ChangedByMemberId);
        Assert.Equal("Second", first.Items[1].Content.Title);
        Assert.Equal("Third", secondPage.Items[0].Content.Title);
        Assert.Equal("R-01", first.Items[1].Identifier);
        Assert.Equal("R-01", secondPage.Items[0].Identifier);
        Assert.Equal(["Other"], otherPage.Items.Select(item => item.Content.Title));
    }

    [Fact]
    public async Task ShouldRejectMalformedAndCrossRecordCursorsGivenRiskDraftHistoryList()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var firstSource = CreateSource(tenantId, programId, "R-01");
        var secondSource = CreateSource(tenantId, programId, "R-02");
        var now = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        var firstCreated = new RiskDraftCreated(tenantId, programId, firstSource.Id,
            Uuid.CreateVersion4(), "R-01", Content("Initial"), Uuid.CreateVersion4(),
            "Author", now);
        var firstRevised = new RiskDraftRevised(tenantId, programId, firstSource.Id, 2,
            Content("Second"), Uuid.CreateVersion4(), "Editor", now.AddMinutes(1));
        var secondCreated = new RiskDraftCreated(tenantId, programId, secondSource.Id,
            Uuid.CreateVersion4(), "R-02", Content("Other"), Uuid.CreateVersion4(),
            "Author", now);
        var directory = new FitzRiskDraftHistoryDirectoryV1(new InMemoryKvClient());
        await ProjectAsync(directory, tenantId, firstCreated, firstRevised, secondCreated);
        var consistency = new RiskDraftHistoryReadConsistency(directory,
            new SourceReader(firstSource, secondSource));
        var handler = new ListRiskDraftRevisionsHandler(directory, consistency);
        var firstPage = await directory.ListRevisionsAsync(tenantId, firstSource.Id, 1, null);

        // Act
        var malformed = await handler.HandleAsync(Context(new ListRiskDraftRevisions(
            tenantId, programId, firstSource.Id, Cursor: "not-a-cursor")), CancellationToken.None);
        var crossRecord = await handler.HandleAsync(Context(new ListRiskDraftRevisions(
            tenantId, programId, secondSource.Id, Cursor: firstPage.NextCursor)),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(malformed.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(crossRecord.Error).Kind);
    }

    [Fact]
    public async Task ShouldReportLagAndHideOtherProgramOrTenantGivenRiskDraftHistoryList()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var risk = CreateSource(tenantId, programId, "R-01");
        Assert.True(risk.Revise(programId, 1, Content("Revised"), Uuid.CreateVersion4(),
            "Editor", DateTimeOffset.UtcNow).IsSuccess);
        var created = new RiskDraftCreated(tenantId, programId, risk.Id,
            Uuid.CreateVersion4(), "R-01", Content("Initial"), Uuid.CreateVersion4(),
            "Author", new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero));
        var revised = new RiskDraftRevised(tenantId, programId, risk.Id, 2,
            Content("Revised"), Uuid.CreateVersion4(), "Editor", created.ChangedAt.AddMinutes(1));
        var projected = new FitzRiskDraftHistoryDirectoryV1(new InMemoryKvClient());
        var directory = new TrackingDirectory(projected);
        var consistency = new RiskDraftHistoryReadConsistency(directory,
            new SourceReader(risk));
        var handler = new ListRiskDraftRevisionsHandler(directory, consistency);

        // Act
        var lagged = await handler.HandleAsync(Context(new ListRiskDraftRevisions(tenantId,
            programId, risk.Id, MinimumRiskRevision: 2)), CancellationToken.None);
        await ProjectAsync(projected, tenantId, created, revised);
        var recovered = await handler.HandleAsync(Context(new ListRiskDraftRevisions(tenantId,
            programId, risk.Id, MinimumRiskRevision: 2)), CancellationToken.None);
        var readsAfterRecovery = directory.RevisionReads;
        var otherProgram = await handler.HandleAsync(Context(new ListRiskDraftRevisions(tenantId,
            Uuid.CreateVersion4(), risk.Id)), CancellationToken.None);
        var otherTenant = await handler.HandleAsync(Context(new ListRiskDraftRevisions(otherTenantId,
            programId, risk.Id)), CancellationToken.None);

        // Assert
        var lagError = Assert.IsType<RequestError>(lagged.Error);
        Assert.Equal(RequestErrorKind.Conflict, lagError.Kind);
        Assert.True(lagError.IsTransient);
        Assert.True(recovered.IsSuccess);
        Assert.Equal([1L, 2L], recovered.Value.Items.Select(item => item.Revision));
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(otherProgram.Error).Kind);
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(otherTenant.Error).Kind);
        Assert.Equal(1, directory.ListCalls);
        Assert.Equal(readsAfterRecovery, directory.RevisionReads);
    }

    static RequestContext<T> Context<T>(T request) where T : IRequestBase =>
        new(request, new ClaimsPrincipal());

    static RiskDraftContent Content(string title) => new(title, $"{title} scenario",
        $"{title} effect", "Management observation");

    static RiskDraft CreateSource(Uuid tenantId, Uuid programId, string identifier)
    {
        var source = new RiskDraft(tenantId, RiskDraft.IdFor(tenantId, programId, identifier));
        Assert.True(source.Create(programId, Uuid.CreateVersion4(), identifier, Content(identifier),
            Uuid.CreateVersion4(), "Author", DateTimeOffset.UtcNow).IsSuccess);
        return source;
    }

    static CheckpointIdentity Identity(Uuid tenantId) => new("RiskDraftHistoryDirectoryV1",
        EventStreamPattern.ForPattern(tenantId.ToString(), "risks"));

    static async Task ProjectAsync(FitzRiskDraftHistoryDirectoryV1 directory, Uuid tenantId,
        params DomainEvent[] events)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            Identity(tenantId), ProjectionCheckpoint.Start));
        foreach (var domainEvent in events)
            await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    sealed class SourceReader(params RiskDraft[] sources) : IAggregateReader
    {
        readonly Dictionary<(string Realm, Uuid RiskId), RiskDraft> _sources = sources
            .ToDictionary(source => (source.Stream.Realm, source.Id));

        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            if (aggregate is RiskDraft requested && _sources.TryGetValue(
                    (requested.Stream.Realm, requested.Id), out var source))
                return ValueTask.FromResult((TAggregate)(Aggregate)source);
            return ValueTask.FromResult(aggregate);
        }
    }

    sealed class TrackingDirectory(IRiskDraftHistoryDirectoryReader inner)
        : IRiskDraftHistoryDirectoryReader
    {
        public int ListCalls { get; private set; }
        public int RevisionReads { get; private set; }

        public ValueTask<RiskDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid riskId,
            long revision, CancellationToken ct = default)
        {
            RevisionReads++;
            return inner.GetRevisionAsync(tenantId, riskId, revision, ct);
        }

        public ValueTask<Page<RiskDraftRevisionView>> ListRevisionsAsync(Uuid tenantId,
            Uuid riskId, int limit, string? cursor, CancellationToken ct = default)
        {
            ListCalls++;
            return inner.ListRevisionsAsync(tenantId, riskId, limit, cursor, ct);
        }
    }
}
