using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

public sealed class ListTrainingRequirementsHandler(IAggregateReader reader)
    : IRequestHandler<ListTrainingRequirements, Page<TrainingRequirementView>>
{
    public async ValueTask<Result<Page<TrainingRequirementView>>> HandleAsync(
        IRequestContext<ListTrainingRequirements> context, CancellationToken ct)
    {
        var request = context.Request;
        var catalog = await reader.HydrateAsync(new TrainingCatalog(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        return ControlActivationSource.Paginate(catalog.Latest(), request.Limit, request.Cursor,
            "training requirements");
    }
}
