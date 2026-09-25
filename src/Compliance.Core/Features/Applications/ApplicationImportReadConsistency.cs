using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class ApplicationImportReadConsistency(IApplicationImportDirectoryReader directory,
    IAggregateReader reader)
{
    public async ValueTask<Result<ApplicationImportView>> GetFreshAsync(Uuid tenantId,
        Uuid batchId, long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result<ApplicationImportView>.Failure(new RequestError(
                RequestErrorKind.Validation, "The minimum import revision must be positive."));
        var source = await reader.HydrateAsync(new ImportBatch(tenantId, batchId), ct)
            .ConfigureAwait(false);
        if (!source.IsCreated)
            return Result<ApplicationImportView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The import batch was not found."));
        if (minimumRevision is { } minimum && source.Revision < minimum)
            return Result<ApplicationImportView>.Failure(new RequestError(
                RequestErrorKind.Conflict, $"The import source has not reached revision {minimum}.",
                isTransient: true));
        var view = await directory.GetAsync(tenantId, batchId, ct).ConfigureAwait(false);
        return view is null || view.TenantId != tenantId || view.BatchId != batchId ||
               view.Revision < source.Revision ||
               (minimumRevision is { } wanted && view.Revision < wanted)
            ? Result<ApplicationImportView>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The import projection has not reached the source revision.",
                isTransient: true))
            : Result<ApplicationImportView>.Success(view);
    }
}
