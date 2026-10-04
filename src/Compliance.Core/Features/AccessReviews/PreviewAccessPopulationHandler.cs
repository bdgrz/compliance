using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications;

namespace Bdgrz.Compliance.Features.AccessReviews;

public sealed class PreviewAccessPopulationHandler(IAggregateReader reader,
    RestrictedApplicationVisibility visibility)
    : IRequestHandler<PreviewAccessPopulation, AccessPopulationPreview>
{
    public async ValueTask<Result<AccessPopulationPreview>> HandleAsync(
        IRequestContext<PreviewAccessPopulation> context, CancellationToken ct)
    {
        var request = context.Request;
        var population = await reader.HydrateAsync(new AccessPopulation(request.TenantId,
            request.PopulationId), ct).ConfigureAwait(false);
        if (population.Opened is not { } opened)
            return AccessReviewOutcome.Failure<AccessPopulationPreview>(RequestErrorKind.NotFound,
                "The population was not found.");
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, userId,
                opened.ApplicationId, opened.SystemInstanceId, ct)
            .ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessPopulationPreview>(RequestErrorKind.NotFound,
                "The population was not found.");
        var issues = AccessPopulationAnalysis.Validate(population.Facts);
        return Result<AccessPopulationPreview>.Success(new AccessPopulationPreview(population.Id,
            population.Revision, issues, AccessPopulationAnalysis.Derive(population.Facts),
            issues.Count == 0 && !population.IsAccepted));
    }
}
