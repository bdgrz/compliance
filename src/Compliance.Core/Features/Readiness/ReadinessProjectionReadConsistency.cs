using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
/// Fences readiness reads against the source cursors of the projections that are consulted
/// outside the core readiness directories.
/// </summary>
sealed class ReadinessProjectionReadConsistency(IInventoryRegisterReader inventoryRegister,
    IAccessReviewScopeDirectoryReader accessReviewScopes, ISnapshotDirectoryReader snapshots,
    IRiskDraftHistoryDirectoryReader riskHistory, IDomainEventReader events)
{
    public ValueTask<Result<ProjectionCheckpoint>> CaptureRiskHistoryAsync(Uuid tenantId,
        CancellationToken ct) => CaptureAsync(
        token => riskHistory.LoadCheckpointAsync(tenantId, token),
        EventStreamPattern.ForPattern(tenantId.ToString(), "risks"), "risk history", ct);

    public ValueTask<Result> ConfirmRiskHistoryUnchangedAndCaughtUpAsync(Uuid tenantId,
        ProjectionCheckpoint fence, CancellationToken ct) => ConfirmAsync(
        token => riskHistory.LoadCheckpointAsync(tenantId, token),
        EventStreamPattern.ForPattern(tenantId.ToString(), "risks"), fence, "risk history", ct);

    public ValueTask<Result<ProjectionCheckpoint>> CaptureSnapshotsAsync(Uuid tenantId,
        CancellationToken ct) => CaptureAsync(
        token => snapshots.LoadCheckpointAsync(tenantId, token),
        EventStreamPattern.ForPattern(tenantId.ToString(), "snapshots"), "program snapshot", ct);

    public ValueTask<Result> ConfirmSnapshotsUnchangedAndCaughtUpAsync(Uuid tenantId,
        ProjectionCheckpoint fence, CancellationToken ct) => ConfirmAsync(
        token => snapshots.LoadCheckpointAsync(tenantId, token),
        EventStreamPattern.ForPattern(tenantId.ToString(), "snapshots"), fence,
        "program snapshot", ct);

    public ValueTask<Result<ProjectionCheckpoint>> CaptureInventoryRegisterAsync(Uuid tenantId,
        CancellationToken ct) => CaptureAsync(
        token => inventoryRegister.LoadCheckpointAsync(tenantId, token),
        InventoryRegisters.TenantPattern(tenantId), "inventory register", ct);

    public ValueTask<Result> ConfirmInventoryRegisterUnchangedAndCaughtUpAsync(Uuid tenantId,
        ProjectionCheckpoint fence, CancellationToken ct) => ConfirmAsync(
        token => inventoryRegister.LoadCheckpointAsync(tenantId, token),
        InventoryRegisters.TenantPattern(tenantId), fence, "inventory register", ct);

    public ValueTask<Result<ProjectionCheckpoint>> CaptureAccessReviewScopesAsync(Uuid tenantId,
        CancellationToken ct) => CaptureAsync(
        token => accessReviewScopes.LoadCheckpointAsync(tenantId, token),
        AccessReviewScopeStreams.TenantPattern(tenantId), "access review scope", ct);

    public ValueTask<Result> ConfirmAccessReviewScopesUnchangedAndCaughtUpAsync(Uuid tenantId,
        ProjectionCheckpoint fence, CancellationToken ct) => ConfirmAsync(
        token => accessReviewScopes.LoadCheckpointAsync(tenantId, token),
        AccessReviewScopeStreams.TenantPattern(tenantId), fence, "access review scope", ct);

    async ValueTask<Result<ProjectionCheckpoint>> CaptureAsync(
        Func<CancellationToken, ValueTask<ProjectionCheckpoint>> loadCheckpoint,
        EventStreamPattern pattern, string source, CancellationToken ct)
    {
        var checkpoint = await loadCheckpoint(ct).ConfigureAwait(false);
        return await HasPendingSourceAsync(pattern, checkpoint, ct).ConfigureAwait(false)
            ? Result<ProjectionCheckpoint>.Failure(BehindSourceError(source))
            : Result<ProjectionCheckpoint>.Success(checkpoint);
    }

    async ValueTask<Result> ConfirmAsync(
        Func<CancellationToken, ValueTask<ProjectionCheckpoint>> loadCheckpoint,
        EventStreamPattern pattern, ProjectionCheckpoint fence, string source,
        CancellationToken ct)
    {
        var checkpoint = await loadCheckpoint(ct).ConfigureAwait(false);
        return checkpoint != fence ||
               await HasPendingSourceAsync(pattern, checkpoint, ct).ConfigureAwait(false)
            ? Result.Failure(BehindSourceError(source))
            : Result.Success;
    }

    async ValueTask<bool> HasPendingSourceAsync(EventStreamPattern pattern,
        ProjectionCheckpoint checkpoint, CancellationToken ct)
    {
        await using var pending = events.ReadAsync(pattern, checkpoint.Cursor, ct)
            .GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false);
    }

    static RequestError BehindSourceError(string source) => new(RequestErrorKind.Conflict,
        $"The {source} projection changed or has not reached the source. Retry the query.",
        isTransient: true);
}
