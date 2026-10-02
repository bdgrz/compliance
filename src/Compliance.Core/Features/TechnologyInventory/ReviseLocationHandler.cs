using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class ReviseLocationHandler(IAggregateExecutor executor,
    TechnologyInventoryReferences references, TimeProvider clock)
    : IRequestHandler<ReviseLocation>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseLocation> context,
        CancellationToken ct)
    {
        var actor = TechnologyInventoryActor.From(context);
        var request = context.Request;
        var owner = await references.ValidateOwnerAsync(request.TenantId,
            request.OwnerPersonId, ct).ConfigureAwait(false);
        if (owner is not null)
            return Result.Failure(owner);
        return await executor.ExecuteAsync(new LocationRegister(request.TenantId),
            register => CommandFailureRequestAdapter.ToOutcome(register.Revise(
                request.LocationId, request.ExpectedRevision, current => current with
                {
                    Name = request.Name,
                    OwnerPersonId = request.OwnerPersonId,
                    Lifecycle = request.Lifecycle,
                    GeographyReference = request.GeographyReference,
                }, actor, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
