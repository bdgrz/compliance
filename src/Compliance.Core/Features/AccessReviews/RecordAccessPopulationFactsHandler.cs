using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications;

namespace Bdgrz.Compliance.Features.AccessReviews;

public sealed class RecordAccessPopulationFactsHandler(IAggregateExecutor executor,
    TimeProvider clock, IAggregateReader reader, RestrictedApplicationVisibility visibility)
    : IRequestHandler<RecordAccessPopulationFacts, AccessPopulationRegistration>
{
    public async ValueTask<Result<AccessPopulationRegistration>> HandleAsync(
        IRequestContext<RecordAccessPopulationFacts> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = AccessReviewActor.From(context);
        if (!await RestrictedAccessReviewVisibility.CanReadPopulationAsync(reader, visibility,
                request.TenantId, actor.UserId, request.PopulationId, ct).ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessPopulationRegistration>(
                RequestErrorKind.NotFound, "The population was not found.");
        var facts = new AccessPopulationFacts(request.Principals, request.Entitlements,
            request.GroupMembers, request.Assignments);
        return await executor.ExecuteAsync(new AccessPopulation(request.TenantId, request.PopulationId),
            population => AccessReviewOutcome.From(population.Record(request.ExpectedRevision, facts,
                actor.Reference, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
