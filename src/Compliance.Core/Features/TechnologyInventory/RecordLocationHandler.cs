using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class RecordLocationHandler(IAggregateExecutor executor,
    TechnologyInventoryReferences references, TimeProvider clock)
    : IRequestHandler<RecordLocation, LocationRegistration>
{
    public async ValueTask<Result<LocationRegistration>> HandleAsync(
        IRequestContext<RecordLocation> context, CancellationToken ct)
    {
        var actor = TechnologyInventoryActor.From(context);
        var request = context.Request;
        var owner = await references.ValidateOwnerAsync(request.TenantId,
            request.OwnerPersonId, ct).ConfigureAwait(false);
        if (owner is not null)
            return Result<LocationRegistration>.Failure(owner);
        var content = new LocationContent(request.Kind, request.Name,
            request.GeographyReference, request.OwnerPersonId, TechnologyInventoryRules.Active);
        // The request ID is the stable location ID, so a retry replays rather than duplicates.
        return await executor.ExecuteAsync(new LocationRegister(request.TenantId),
            register => AggregateOutcome.CommitOnSuccess(register.Record(context.RequestId,
                content, actor, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
