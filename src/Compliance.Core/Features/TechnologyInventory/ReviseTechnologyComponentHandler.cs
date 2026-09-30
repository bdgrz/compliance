using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class ReviseTechnologyComponentHandler(IAggregateExecutor executor,
    TechnologyInventoryReferences references, TimeProvider clock)
    : IRequestHandler<ReviseTechnologyComponent>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseTechnologyComponent> context,
        CancellationToken ct)
    {
        var actor = TechnologyInventoryActor.From(context);
        var request = context.Request;
        var owner = await references.ValidateOwnerAsync(request.TenantId,
            request.OwnerPersonId, ct).ConfigureAwait(false);
        if (owner is not null)
            return Result.Failure(owner);
        return await executor.ExecuteAsync(new TechnologyComponent(request.TenantId,
                request.ComponentId),
            component => CommandFailureRequestAdapter.ToOutcome(component.Revise(
                request.ExpectedRevision, current => current with
                {
                    Name = request.Name,
                    OwnerPersonId = request.OwnerPersonId,
                    Lifecycle = request.Lifecycle,
                    EnvironmentReference = request.EnvironmentReference,
                    LocationReference = request.LocationReference,
                    EndpointCount = request.EndpointCount,
                    ManagementSource = request.ManagementSource,
                }, actor, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
