using System.Runtime.CompilerServices;
using System.Security.Claims;
using Bdgrz.Compliance.Features.Policies;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Policies;

public sealed class ListPoliciesHandlerTests
{
    [Fact]
    public async Task ShouldReturnTransientConflictGivenProjectionAdvancesDuringListRead()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var directory = new ScriptedDirectory(tenantId, programId);
        var handler = new ListPoliciesHandler(directory,
            new PolicyDirectoryReadConsistency(directory, new ScriptedEventReader()), TimeProvider.System);
        var context = new RequestContext<ListPolicies>(new ListPolicies(tenantId, programId),
            new ClaimsPrincipal());

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
        Assert.Equal(2, directory.CheckpointReads);
        Assert.Equal(1, directory.ListReads);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenPolicyEventsBeyondProjectionCheckpoint()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var directory = new ScriptedDirectory(tenantId, programId);
        var handler = new ListPoliciesHandler(directory,
            new PolicyDirectoryReadConsistency(directory, new ScriptedEventReader(pendingRead: 1)),
            TimeProvider.System);
        var context = new RequestContext<ListPolicies>(new ListPolicies(tenantId, programId),
            new ClaimsPrincipal());

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
        Assert.Equal(1, directory.CheckpointReads);
        Assert.Equal(0, directory.ListReads);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenPolicyEventArrivesDuringListRead()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var directory = new ScriptedDirectory(tenantId, programId, advanceOnConfirm: false);
        var handler = new ListPoliciesHandler(directory,
            new PolicyDirectoryReadConsistency(directory,
                new ScriptedEventReader(pendingRead: 2)), TimeProvider.System);
        var context = new RequestContext<ListPolicies>(new ListPolicies(tenantId, programId),
            new ClaimsPrincipal());

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
        Assert.Equal(2, directory.CheckpointReads);
        Assert.Equal(1, directory.ListReads);
    }

    [Fact]
    public async Task ShouldReturnPoliciesGivenStableCaughtUpProjection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var directory = new ScriptedDirectory(tenantId, programId, advanceOnConfirm: false);
        var handler = new ListPoliciesHandler(directory,
            new PolicyDirectoryReadConsistency(directory, new ScriptedEventReader()), TimeProvider.System);
        var context = new RequestContext<ListPolicies>(new ListPolicies(tenantId, programId),
            new ClaimsPrincipal());

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        Assert.Equal(2, directory.CheckpointReads);
        Assert.Equal(1, directory.ListReads);
    }

    sealed class ScriptedDirectory(Uuid tenantId, Uuid programId, bool advanceOnConfirm = true)
        : IPolicyDirectoryReader
    {
        public int CheckpointReads { get; private set; }
        public int ListReads { get; private set; }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid requestedTenantId,
            CancellationToken ct = default)
        {
            Assert.Equal(tenantId, requestedTenantId);
            CheckpointReads++;
            return ValueTask.FromResult(advanceOnConfirm && CheckpointReads > 1
                ? new ProjectionCheckpoint(new EventCursor("advanced"))
                : ProjectionCheckpoint.Start);
        }

        public ValueTask<Page<PolicySummaryView>> ListProgramAsync(Uuid requestedTenantId,
            Uuid requestedProgramId, int limit, string? cursor, CancellationToken ct = default)
        {
            Assert.Equal(tenantId, requestedTenantId);
            Assert.Equal(programId, requestedProgramId);
            ListReads++;
            return ValueTask.FromResult(new Page<PolicySummaryView>([], null));
        }
    }

    sealed class ScriptedEventReader(int pendingRead = int.MaxValue) : IDomainEventReader
    {
        int _patternReads;

        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream,
            ulong fromVersion, [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern,
            EventCursor after, [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            _patternReads++;
            if (_patternReads == pendingRead)
                yield return null!;
        }
    }
}
