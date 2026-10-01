using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Opens a draft against the exact current revision of an active system instance.</summary>
public sealed class OpenAccessPopulationHandler(IAggregateExecutor executor,
    IAggregateReader reader, IDomainEventReader events, TimeProvider clock)
    : IRequestHandler<OpenAccessPopulation, AccessPopulationRegistration>
{
    public async ValueTask<Result<AccessPopulationRegistration>> HandleAsync(
        IRequestContext<OpenAccessPopulation> context, CancellationToken ct)
    {
        var request = context.Request;
        var instance = await ScopedSystemInstanceSource.FindAsync(reader, events, request.TenantId,
            request.ApplicationId, request.SystemInstanceId, ct).ConfigureAwait(false);
        if (instance is null)
            return AccessReviewOutcome.Failure<AccessPopulationRegistration>(
                RequestErrorKind.NotFound, "The system instance was not found.");
        if (instance.IsRetired)
            return AccessReviewOutcome.Failure<AccessPopulationRegistration>(
                RequestErrorKind.Conflict, "A retired system instance cannot receive a new population.");
        if (instance.Revision != request.ExpectedSystemInstanceRevision)
            return AccessReviewOutcome.Failure<AccessPopulationRegistration>(
                RequestErrorKind.Conflict,
                $"The system instance is at revision {instance.Revision}; the population names revision {request.ExpectedSystemInstanceRevision}.");
        var actor = AccessReviewActor.From(context);
        return await executor.ExecuteAsync(new AccessPopulation(request.TenantId, context.RequestId),
            population => AccessReviewOutcome.From(population.Open(request.ApplicationId,
                request.SystemInstanceId, instance.Revision, request.ObservedAt, request.Source,
                actor.Reference, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
