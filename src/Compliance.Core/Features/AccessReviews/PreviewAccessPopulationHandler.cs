using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

public sealed class PreviewAccessPopulationHandler(IAggregateReader reader)
    : IRequestHandler<PreviewAccessPopulation, AccessPopulationPreview>
{
    public async ValueTask<Result<AccessPopulationPreview>> HandleAsync(
        IRequestContext<PreviewAccessPopulation> context, CancellationToken ct)
    {
        var request = context.Request;
        var population = await reader.HydrateAsync(new AccessPopulation(request.TenantId,
            request.PopulationId), ct).ConfigureAwait(false);
        if (!population.IsOpened)
            return AccessReviewOutcome.Failure<AccessPopulationPreview>(RequestErrorKind.NotFound,
                "The population was not found.");
        var issues = AccessPopulationAnalysis.Validate(population.Facts);
        return Result<AccessPopulationPreview>.Success(new AccessPopulationPreview(population.Id,
            population.Revision, issues, AccessPopulationAnalysis.Derive(population.Facts),
            issues.Count == 0 && !population.IsAccepted));
    }
}
