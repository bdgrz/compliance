using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>Resolves risk scope and the platform member a workforce person is correlated with.</summary>
static class RiskOwnerResolution
{
    public static async ValueTask<(bool Exists, Uuid? MemberId)> ResolveAsync(
        IAggregateReader reader, Uuid tenantId, Uuid personId, CancellationToken ct)
    {
        var person = await reader.HydrateAsync(new Person(tenantId, personId), ct)
            .ConfigureAwait(false);
        return (person.IsCreated, person.CorrelatedUserId is { } userId
            ? RbacIds.Member(tenantId, userId)
            : null);
    }

    public static async ValueTask<Result> RequireRiskAsync(IAggregateReader reader,
        Uuid tenantId, Uuid programId, Uuid riskId, CancellationToken ct)
    {
        var risk = await reader.HydrateAsync(new RiskDraft(tenantId, riskId), ct)
            .ConfigureAwait(false);
        return risk.IsCreated && risk.ProgramId == programId
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.NotFound, "The risk was not found."));
    }
}
