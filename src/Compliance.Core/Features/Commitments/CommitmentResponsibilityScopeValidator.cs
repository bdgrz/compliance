using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

sealed class CommitmentResponsibilityScopeValidator(IAggregateReader reader)
{
    public async ValueTask<Result> ValidateAsync(Uuid tenantId, ResponsibilityScope scope,
        CancellationToken ct = default)
    {
        var draft = await reader.HydrateAsync(new CommitmentDraft(tenantId, scope.RecordId), ct)
            .ConfigureAwait(false);
        if (!draft.IsCreated)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The responsibility source record was not found."));
        return draft.IsCurrentResponsibilityScope(scope)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Responsibilities must target the current exact pending commitment revision."));
    }
}
