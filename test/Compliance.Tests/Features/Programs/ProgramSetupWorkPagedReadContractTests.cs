using System.Security.Claims;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Programs;

public sealed class ProgramSetupWorkPagedReadContractTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task ShouldRejectOutOfRangeBoundaryLimitGivenSetupWork(int limit)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handler = Handler(tenantId, programId, new BoundaryDirectory());

        // Act
        var result = await handler.HandleAsync(Context(new GetProgramSetupWork(tenantId,
            programId, BoundaryLimit: limit)), CancellationToken.None);

        // Assert
        AssertValidation(result.Error);
    }

    [Fact]
    public async Task ShouldRejectInvalidBoundaryCursorGivenSetupWork()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handler = Handler(tenantId, programId, new BoundaryDirectory { RejectCursor = true });

        // Act
        var result = await handler.HandleAsync(Context(new GetProgramSetupWork(tenantId,
            programId, BoundaryCursor: "invalid")), CancellationToken.None);

        // Assert
        AssertValidation(result.Error);
    }

    [Fact]
    public async Task ShouldRejectForeignBoundaryContentGivenSetupWork()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var foreign = new BoundaryView(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), null, null, null);
        var handler = Handler(tenantId, programId, new BoundaryDirectory { Page = new([foreign], null) });

        // Act
        var result = await handler.HandleAsync(Context(new GetProgramSetupWork(tenantId,
            programId)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
    }

    static GetProgramSetupWorkHandler Handler(Uuid tenantId, Uuid programId,
        IBoundaryDirectoryReader boundaries) => new(new ProgramDirectory(Program(tenantId, programId)),
        boundaries, new AggregateReader());

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
        public Page<BoundaryView> Page { get; init; } = new([], null);
        public bool RejectCursor { get; init; }

        public ValueTask<BoundaryView?> GetAsync(Uuid tenantId, Uuid boundaryId,
            CancellationToken ct = default) => ValueTask.FromResult<BoundaryView?>(null);

        public ValueTask<Page<BoundaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) =>
            RejectCursor
                ? ValueTask.FromException<Page<BoundaryView>>(new KvDirectoryQueryException())
                : ValueTask.FromResult(Page);

        public ValueTask<BoundaryVersionView?> GetVersionAsync(Uuid tenantId, Uuid boundaryId,
            Uuid versionId, CancellationToken ct = default) =>
            ValueTask.FromResult<BoundaryVersionView?>(null);

        public ValueTask<Page<BoundaryVersionView>?> ListVersionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<BoundaryVersionView>?>(null);

        public ValueTask<BoundaryVersionView?> GetEffectiveVersionAsync(Uuid tenantId,
            Uuid boundaryId, DateOnly effectiveOn, CancellationToken ct = default) =>
            ValueTask.FromResult<BoundaryVersionView?>(null);

        public ValueTask<BoundaryDecisionView?> GetDecisionAsync(Uuid tenantId,
            Uuid boundaryId, Uuid decisionId, CancellationToken ct = default) =>
            ValueTask.FromResult<BoundaryDecisionView?>(null);

        public ValueTask<Page<BoundaryDecisionView>?> ListDecisionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<BoundaryDecisionView>?>(null);
    }

    sealed class AggregateReader : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregate);
    }
}
