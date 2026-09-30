using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class RecordInformationAssetHandler(IAggregateExecutor executor,
    TechnologyInventoryReferences references, TimeProvider clock)
    : IRequestHandler<RecordInformationAsset, InformationAssetRegistration>
{
    public async ValueTask<Result<InformationAssetRegistration>> HandleAsync(
        IRequestContext<RecordInformationAsset> context, CancellationToken ct)
    {
        var actor = TechnologyInventoryActor.From(context);
        var request = context.Request;
        var owner = await references.ValidateOwnerAsync(request.TenantId,
            request.OwnerPersonId, ct).ConfigureAwait(false);
        if (owner is not null)
            return Result<InformationAssetRegistration>.Failure(owner);
        var content = new InformationAssetContent(request.Name, request.Classification,
            request.RetentionReference, request.OwnerPersonId, request.Description,
            TechnologyInventoryRules.Active);
        return await executor.ExecuteAsync(new InformationAsset(request.TenantId,
                context.RequestId),
            asset => AggregateOutcome.CommitOnSuccess(asset.Record(content, actor,
                clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
