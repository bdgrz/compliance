using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class RecordDataFlowHandler(IAggregateExecutor executor,
    TechnologyInventoryReferences references, TimeProvider clock)
    : IRequestHandler<RecordDataFlow, DataFlowRegistration>
{
    public async ValueTask<Result<DataFlowRegistration>> HandleAsync(
        IRequestContext<RecordDataFlow> context, CancellationToken ct)
    {
        var actor = TechnologyInventoryActor.From(context);
        var request = context.Request;
        var owner = await references.ValidateOwnerAsync(request.TenantId,
            request.OwnerPersonId, ct).ConfigureAwait(false);
        if (owner is not null)
            return Result<DataFlowRegistration>.Failure(owner);
        var classification = await references.ResolveFlowAsync(request.TenantId,
            request.SourceType, request.SourceId, request.DestinationType,
            request.DestinationId, request.InformationAssetIds, ct).ConfigureAwait(false);
        if (!classification.IsSuccess)
            return Result<DataFlowRegistration>.Failure(classification.Error);
        var content = new DataFlowContent(request.SourceType, request.SourceId,
            request.DestinationType, request.DestinationId, request.DestinationParty,
            request.InformationAssetIds, request.Purpose, request.EncryptedInTransit,
            request.EncryptedAtRest, request.ExceptionReference, request.EffectiveFrom,
            request.OwnerPersonId, TechnologyInventoryRules.Active, classification.Value);
        return await executor.ExecuteAsync(new DataFlow(request.TenantId, context.RequestId),
            flow => AggregateOutcome.CommitOnSuccess(flow.Record(content, actor,
                clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
