using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class RiskDraftReadConsistency(IRiskDraftDirectoryReader directory,
    IAggregateReader reader)
{
    public async ValueTask<Result<RiskDraftView>> GetAsync(Uuid tenantId, Uuid programId,
        Uuid riskId, long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result<RiskDraftView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum risk draft revision must be positive."));
        var source = await reader.HydrateAsync(new RiskDraft(tenantId, riskId), ct)
            .ConfigureAwait(false);
        if (!source.IsCreated || source.ProgramId != programId)
            return Result<RiskDraftView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The risk draft was not found."));
        var view = await directory.GetAsync(tenantId, riskId, ct).ConfigureAwait(false);
        if (view is not null && view.TenantId == tenantId && view.ProgramId == programId &&
            view.RiskId == riskId &&
            view.Revision >= source.Revision &&
            (minimumRevision is null || view.Revision >= minimumRevision))
            return Result<RiskDraftView>.Success(view);
        return Result<RiskDraftView>.Failure(new RequestError(RequestErrorKind.Conflict,
            minimumRevision is { } minimum && source.Revision < minimum
                ? $"The risk draft source has not reached revision {minimum}."
                : "The risk draft projection has not reached the requested revision.",
            isTransient: true));
    }
}
