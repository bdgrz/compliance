using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class ReviseDataFlowHandler(IAggregateExecutor executor,
    TechnologyInventoryReferences references, TimeProvider clock)
    : IRequestHandler<ReviseDataFlow>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseDataFlow> context,
        CancellationToken ct)
    {
        var actor = TechnologyInventoryActor.From(context);
        var request = context.Request;
        var owner = await references.ValidateOwnerAsync(request.TenantId,
            request.OwnerPersonId, ct).ConfigureAwait(false);
        if (owner is not null)
            return Result.Failure(owner);
        var classification = await references.ResolveFlowAsync(request.TenantId,
            request.SourceType, request.SourceId, request.DestinationType,
            request.DestinationId, request.InformationAssetIds, ct).ConfigureAwait(false);
        if (!classification.IsSuccess)
            return Result.Failure(classification.Error);
        return await executor.ExecuteAsync(new DataFlow(request.TenantId, request.DataFlowId),
            flow => CommandFailureRequestAdapter.ToOutcome(flow.Revise(request.ExpectedRevision,
                _ => new DataFlowContent(request.SourceType, request.SourceId,
                    request.DestinationType, request.DestinationId, request.DestinationParty,
                    request.InformationAssetIds, request.Purpose, request.EncryptedInTransit,
                    request.EncryptedAtRest, request.ExceptionReference, request.EffectiveFrom,
                    request.OwnerPersonId, request.Lifecycle, classification.Value),
                actor, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
