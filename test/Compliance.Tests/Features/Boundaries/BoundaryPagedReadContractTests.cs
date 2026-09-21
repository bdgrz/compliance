using System.Security.Claims;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Boundaries;

public sealed class BoundaryPagedReadContractTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task ShouldRejectOutOfRangeLimitGivenBoundaryLists(int limit)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var directory = new BoundaryDirectory();
        var programDirectory = new ProgramDirectory(Program(tenantId, programId));
        var consistency = new BoundaryHistoryReadConsistency(directory,
            new AggregateReader(new SystemBoundary(tenantId, boundaryId)));
        var boundaries = new ListProgramBoundariesHandler(directory, programDirectory);
        var versions = new ListBoundaryVersionsHandler(directory, consistency);
        var decisions = new ListBoundaryDecisionsHandler(directory);

        // Act
        var boundaryResult = await boundaries.HandleAsync(Context(new ListProgramBoundaries(
            tenantId, programId, limit)), CancellationToken.None);
        var versionResult = await versions.HandleAsync(Context(new ListBoundaryVersions(
            tenantId, boundaryId, limit)), CancellationToken.None);
        var decisionResult = await decisions.HandleAsync(Context(new ListBoundaryDecisions(
            tenantId, boundaryId, limit)), CancellationToken.None);

        // Assert
        AssertValidation(boundaryResult.Error);
        AssertValidation(versionResult.Error);
        AssertValidation(decisionResult.Error);
    }

    [Fact]
    public async Task ShouldRejectInvalidCursorGivenBoundaryLists()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var directory = new BoundaryDirectory { RejectCursor = true };
        var programDirectory = new ProgramDirectory(Program(tenantId, programId));
        var consistency = new BoundaryHistoryReadConsistency(directory,
            new AggregateReader(new SystemBoundary(tenantId, boundaryId)));
        var boundaries = new ListProgramBoundariesHandler(directory, programDirectory);
        var versions = new ListBoundaryVersionsHandler(directory, consistency);
        var decisions = new ListBoundaryDecisionsHandler(directory);

        // Act
        var boundaryResult = await boundaries.HandleAsync(Context(new ListProgramBoundaries(
            tenantId, programId, Cursor: "invalid")), CancellationToken.None);
        var versionResult = await versions.HandleAsync(Context(new ListBoundaryVersions(
            tenantId, boundaryId, Cursor: "invalid")), CancellationToken.None);
        var decisionResult = await decisions.HandleAsync(Context(new ListBoundaryDecisions(
            tenantId, boundaryId, Cursor: "invalid")), CancellationToken.None);

        // Assert
        AssertValidation(boundaryResult.Error);
        AssertValidation(versionResult.Error);
        AssertValidation(decisionResult.Error);
    }

    [Fact]
    public async Task ShouldHideForeignProgramGivenBoundaryList()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handler = new ListProgramBoundariesHandler(new BoundaryDirectory(),
            new ProgramDirectory(Program(Uuid.CreateVersion4(), programId)));

        // Act
        var result = await handler.HandleAsync(Context(new ListProgramBoundaries(tenantId,
            programId)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    static RequestContext<T> Context<T>(T request) where T : IRequestBase =>
        new(request, new ClaimsPrincipal());

    static void AssertValidation(RequestError? error) =>
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(error).Kind);

    static ProgramView Program(Uuid tenantId, Uuid programId) => new(tenantId, programId,
        "Readiness", "readiness", "type_i", 1,
        new ProgramPlan(null, null, null, null, null, null), Uuid.CreateVersion4(), "Owner",
        DateTimeOffset.UtcNow, []);

    sealed class ProgramDirectory(ProgramView program) : IProgramDirectoryReader
    {
        public ValueTask<ProgramView?> GetAsync(Uuid tenantId, Uuid programId,
            CancellationToken ct = default) => ValueTask.FromResult<ProgramView?>(program);

        public ValueTask<Page<ProgramView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<ProgramView>([], null));

        public ValueTask<Page<ProgramRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<ProgramRevisionView>?>(null);

        public ValueTask<ProgramRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid programId,
            long revision, CancellationToken ct = default) =>
            ValueTask.FromResult<ProgramRevisionView?>(null);
    }

    sealed class BoundaryDirectory : IBoundaryDirectoryReader
    {
        public bool RejectCursor { get; init; }

        public ValueTask<BoundaryView?> GetAsync(Uuid tenantId, Uuid boundaryId,
            CancellationToken ct = default) => ValueTask.FromResult<BoundaryView?>(null);

        public ValueTask<Page<BoundaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) =>
            RejectCursor
                ? ValueTask.FromException<Page<BoundaryView>>(new KvDirectoryQueryException())
                : ValueTask.FromResult(new Page<BoundaryView>([], null));

        public ValueTask<BoundaryVersionView?> GetVersionAsync(Uuid tenantId, Uuid boundaryId,
            Uuid versionId, CancellationToken ct = default) =>
            ValueTask.FromResult<BoundaryVersionView?>(null);

        public ValueTask<Page<BoundaryVersionView>?> ListVersionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            RejectCursor
                ? ValueTask.FromException<Page<BoundaryVersionView>?>(new KvDirectoryQueryException())
                : ValueTask.FromResult<Page<BoundaryVersionView>?>(new Page<BoundaryVersionView>([], null));

        public ValueTask<BoundaryVersionView?> GetEffectiveVersionAsync(Uuid tenantId,
            Uuid boundaryId, DateOnly effectiveOn, CancellationToken ct = default) =>
            ValueTask.FromResult<BoundaryVersionView?>(null);

        public ValueTask<BoundaryDecisionView?> GetDecisionAsync(Uuid tenantId,
            Uuid boundaryId, Uuid decisionId, CancellationToken ct = default) =>
            ValueTask.FromResult<BoundaryDecisionView?>(null);

        public ValueTask<Page<BoundaryDecisionView>?> ListDecisionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            RejectCursor
                ? ValueTask.FromException<Page<BoundaryDecisionView>?>(new KvDirectoryQueryException())
                : ValueTask.FromResult<Page<BoundaryDecisionView>?>(new Page<BoundaryDecisionView>([], null));
    }

    sealed class AggregateReader(Aggregate source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)source);
    }
}
