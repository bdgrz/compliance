using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class ListTechnologyComponentBoundaryReferencesHandler(
    IAggregateReader aggregates, IApplicationBoundaryReferenceDirectory directory,
    ApplicationBoundaryReferenceReadConsistency consistency)
    : IRequestHandler<ListTechnologyComponentBoundaryReferences,
        Page<ApplicationBoundaryReferenceView>>
{
    public async ValueTask<Result<Page<ApplicationBoundaryReferenceView>>> HandleAsync(
        IRequestContext<ListTechnologyComponentBoundaryReferences> context, CancellationToken ct)
    {
        var request = context.Request;
        if (InventoryBoundaryReferenceReader.RejectLimit(request.Limit, "technology component")
            is { } rejected)
            return rejected;
        var component = await aggregates.HydrateAsync(new TechnologyComponent(
            request.TenantId, request.ComponentId), ct).ConfigureAwait(false);
        return await InventoryBoundaryReferenceReader.ListAsync(component.IsCreated,
            "technology component", "component", request.TenantId, request.ComponentId,
            request.Limit, request.Cursor, directory, consistency, ct).ConfigureAwait(false);
    }
}
