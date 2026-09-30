using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class ReviseInformationAssetHandler(IAggregateExecutor executor,
    TechnologyInventoryReferences references, TimeProvider clock)
    : IRequestHandler<ReviseInformationAsset>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseInformationAsset> context,
        CancellationToken ct)
    {
        var actor = TechnologyInventoryActor.From(context);
        var request = context.Request;
        var owner = await references.ValidateOwnerAsync(request.TenantId,
            request.OwnerPersonId, ct).ConfigureAwait(false);
        if (owner is not null)
            return Result.Failure(owner);
        return await executor.ExecuteAsync(new InformationAsset(request.TenantId,
                request.InformationAssetId),
            asset => CommandFailureRequestAdapter.ToOutcome(asset.Revise(
                request.ExpectedRevision, current => current with
                {
                    Name = request.Name,
                    Classification = request.Classification,
                    RetentionReference = request.RetentionReference,
                    OwnerPersonId = request.OwnerPersonId,
                    Description = request.Description,
                    Lifecycle = request.Lifecycle,
                }, actor, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
