using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

public sealed class RecordAccessPopulationFactsHandler(IAggregateExecutor executor,
    TimeProvider clock)
    : IRequestHandler<RecordAccessPopulationFacts, AccessPopulationRegistration>
{
    public async ValueTask<Result<AccessPopulationRegistration>> HandleAsync(
        IRequestContext<RecordAccessPopulationFacts> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = AccessReviewActor.From(context);
        var facts = new AccessPopulationFacts(request.Principals, request.Entitlements,
            request.GroupMembers, request.Assignments);
        return await executor.ExecuteAsync(new AccessPopulation(request.TenantId, request.PopulationId),
            population => AccessReviewOutcome.From(population.Record(request.ExpectedRevision, facts,
                actor.Reference, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
