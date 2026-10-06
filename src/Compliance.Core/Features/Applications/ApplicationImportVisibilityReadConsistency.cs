using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed class ApplicationImportVisibilityReadConsistency(IApplicationDirectoryReader directory,
    IDomainEventReader events)
{
    public async ValueTask<Result<ProjectionCheckpoint>> CaptureAsync(Uuid tenantId, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        return await HasPendingCommitAsync(tenantId, checkpoint, ct).ConfigureAwait(false)
            ? Result<ProjectionCheckpoint>.Failure(BehindSource())
            : Result<ProjectionCheckpoint>.Success(checkpoint);
    }

    public async ValueTask<Result> ConfirmAsync(Uuid tenantId, ProjectionCheckpoint fence, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        return checkpoint != fence || await HasPendingCommitAsync(tenantId, fence, ct).ConfigureAwait(false)
            ? Result.Failure(BehindSource()) : Result.Success;
    }

    async ValueTask<bool> HasPendingCommitAsync(Uuid tenantId, ProjectionCheckpoint checkpoint, CancellationToken ct)
    {
        var count = 0;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString()),
                           checkpoint.Cursor, ct).ConfigureAwait(false))
            if (count++ == ApplicationDirectoryBacklog.ScanLimit || record.Event is ApplicationImportCommitted)
                return true;
        return false;
    }

    static RequestError BehindSource() => new(RequestErrorKind.Conflict,
        "Application import visibility is changing or has not finished projecting. Retry the read.", isTransient: true);
}
