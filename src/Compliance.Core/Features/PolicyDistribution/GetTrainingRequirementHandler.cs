using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

public sealed class GetTrainingRequirementHandler(IAggregateReader reader)
    : IRequestHandler<GetTrainingRequirement, TrainingRequirementView>
{
    public async ValueTask<Result<TrainingRequirementView>> HandleAsync(
        IRequestContext<GetTrainingRequirement> context, CancellationToken ct)
    {
        var request = context.Request;
        var catalog = await reader.HydrateAsync(new TrainingCatalog(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        return catalog.Find(request.RequirementId, request.Version) is { } requirement
            ? Result<TrainingRequirementView>.Success(requirement)
            : Result<TrainingRequirementView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The training requirement was not found."));
    }
}
