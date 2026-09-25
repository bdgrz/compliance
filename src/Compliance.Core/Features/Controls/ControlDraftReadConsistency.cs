using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class ControlDraftReadConsistency(IControlDraftDirectoryReader directory,
    IAggregateReader reader)
{
    public async ValueTask<Result<ControlDraftView>> GetAsync(Uuid tenantId, Uuid programId,
        Uuid controlId, long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result<ControlDraftView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum control draft revision must be positive."));
        var source = await reader.HydrateAsync(new ControlDraft(tenantId, controlId), ct)
            .ConfigureAwait(false);
        if (!source.IsCreated || !source.IsVisible || source.ProgramId != programId)
            return Result<ControlDraftView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The control draft was not found."));
        var view = await directory.GetAsync(tenantId, controlId, ct).ConfigureAwait(false);
        if (view is not null && view.TenantId == tenantId && view.ProgramId == programId &&
            view.ControlId == controlId &&
            view.Revision >= source.Revision &&
            (minimumRevision is null || view.Revision >= minimumRevision))
            return Result<ControlDraftView>.Success(view);
        return Result<ControlDraftView>.Failure(new RequestError(RequestErrorKind.Conflict,
            minimumRevision is { } minimum && source.Revision < minimum
                ? $"The control draft source has not reached revision {minimum}."
                : "The control draft projection has not reached the requested revision.",
            isTransient: true));
    }
}
