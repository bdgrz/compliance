using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class ControlDraftHistoryReadConsistency(
    IControlDraftHistoryDirectoryReader directory, IAggregateReader reader)
{
    public async ValueTask<Result> EnsureAsync(Uuid tenantId, Uuid programId, Uuid controlId,
        long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum control draft revision must be positive."));
        var source = await reader.HydrateAsync(new ControlDraft(tenantId, controlId), ct)
            .ConfigureAwait(false);
        if (!source.IsCreated || !source.IsVisible || source.ProgramId != programId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The control draft was not found."));
        if (minimumRevision is { } minimum && source.Revision < minimum)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The control draft source has not reached revision {minimum}.", isTransient: true));
        var revision = await directory.GetRevisionAsync(tenantId, controlId, source.Revision, ct)
            .ConfigureAwait(false);
        return revision is not null && revision.TenantId == tenantId &&
               revision.ProgramId == programId && revision.ControlId == controlId &&
               revision.Revision == source.Revision
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The control draft history projection has not reached the requested revision.",
                isTransient: true));
    }
}
