using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>Compares the evaluation projection with its event-sourced source revision.</summary>
public sealed class RiskEvaluationReadConsistency(IRiskEvaluationDirectoryReader directory,
    IAggregateReader reader)
{
    public async ValueTask<Result<RiskEvaluationView>> GetAsync(Uuid tenantId, Uuid programId,
        Uuid riskId, long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 0)
            return Result<RiskEvaluationView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum risk evaluation revision cannot be negative."));
        var risk = await reader.HydrateAsync(new RiskDraft(tenantId, riskId), ct)
            .ConfigureAwait(false);
        if (!risk.IsCreated || risk.ProgramId != programId)
            return Result<RiskEvaluationView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The risk was not found."));
        var source = await reader.HydrateAsync(new RiskEvaluation(tenantId, riskId), ct)
            .ConfigureAwait(false);
        if (minimumRevision is { } minimum && source.Revision < minimum)
            return Result<RiskEvaluationView>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The risk evaluation source has not reached revision {minimum}.",
                isTransient: true));
        if (source.Revision == 0)
            return Result<RiskEvaluationView>.Success(new RiskEvaluationView(tenantId, programId,
                riskId, 0, "unassessed", [], null, [], null, null, null));
        var view = await directory.GetAsync(tenantId, riskId, ct).ConfigureAwait(false);
        return view is not null && view.TenantId == tenantId && view.ProgramId == programId &&
               view.RiskId == riskId && view.Revision >= source.Revision
            ? Result<RiskEvaluationView>.Success(view)
            : Result<RiskEvaluationView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The risk evaluation projection has not reached the current source revision.",
                isTransient: true));
    }
}
