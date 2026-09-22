using System.Security.Claims;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Snapshots;

public sealed class SnapshotPagedReadContractTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task ShouldRejectOutOfRangeLimitGivenProgramSnapshots(int limit)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handler = new ListProgramSnapshotsHandler(new SnapshotDirectory(),
            new AggregateReader(Program(tenantId, programId)));

        // Act
        var result = await handler.HandleAsync(Context(new ListProgramSnapshots(tenantId,
            programId, limit)), CancellationToken.None);

        // Assert
        AssertValidation(result.Error);
    }

    [Fact]
    public async Task ShouldRejectInvalidCursorGivenProgramSnapshots()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handler = new ListProgramSnapshotsHandler(new SnapshotDirectory { RejectCursor = true },
            new AggregateReader(Program(tenantId, programId)));

        // Act
        var result = await handler.HandleAsync(Context(new ListProgramSnapshots(tenantId,
            programId, Cursor: "invalid")), CancellationToken.None);

        // Assert
        AssertValidation(result.Error);
    }

    static RequestContext<T> Context<T>(T request) where T : IRequestBase =>
        new(request, new ClaimsPrincipal());

    static void AssertValidation(RequestError? error) =>
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(error).Kind);

    static ComplianceProgram Program(Uuid tenantId, Uuid programId)
    {
        var program = new ComplianceProgram(tenantId, programId);
        Assert.True(program.Create("Readiness", new ProgramPlan(null, null, null, null, null, null),
            Uuid.CreateVersion4(), "Owner", DateTimeOffset.UtcNow).IsSuccess);
        return program;
    }

    sealed class SnapshotDirectory : ISnapshotDirectoryReader
    {
        public bool RejectCursor { get; init; }

        public ValueTask<SnapshotView?> GetAsync(Uuid tenantId, Uuid snapshotId,
            CancellationToken ct = default) => ValueTask.FromResult<SnapshotView?>(null);

        public ValueTask<Page<SnapshotView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) =>
            RejectCursor
                ? ValueTask.FromException<Page<SnapshotView>>(new KvDirectoryQueryException())
                : ValueTask.FromResult(new Page<SnapshotView>([], null));
    }

    sealed class AggregateReader(Aggregate source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)source);
    }
}
