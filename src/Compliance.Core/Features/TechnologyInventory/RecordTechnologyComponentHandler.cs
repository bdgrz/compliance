using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class RecordTechnologyComponentHandler(IAggregateExecutor executor,
    TechnologyInventoryReferences references, TimeProvider clock)
    : IRequestHandler<RecordTechnologyComponent, TechnologyComponentRegistration>
{
    public async ValueTask<Result<TechnologyComponentRegistration>> HandleAsync(
        IRequestContext<RecordTechnologyComponent> context, CancellationToken ct)
    {
        var actor = TechnologyInventoryActor.From(context);
        var request = context.Request;
        var content = new TechnologyComponentContent(request.Category, request.Name,
            request.OwnerPersonId, request.EnvironmentReference, request.LocationReference,
            request.SystemInstanceId, request.EndpointCount, request.ManagementSource,
            TechnologyInventoryRules.Active);
        var error = await references.ValidateOwnerAsync(request.TenantId,
                request.OwnerPersonId, ct).ConfigureAwait(false) ??
            (request.Category?.Trim() == TechnologyInventoryRules.CloudAccount
                ? await references.ValidateCloudAccountAsync(request.TenantId,
                    request.SystemInstanceId, ct).ConfigureAwait(false)
                : null);
        if (error is not null)
            return Result<TechnologyComponentRegistration>.Failure(error);
        // A cloud account component is keyed by its system instance so it cannot be duplicated.
        var id = request.Category?.Trim() == TechnologyInventoryRules.CloudAccount &&
                 request.SystemInstanceId is { } instanceId && instanceId != Uuid.Empty
            ? instanceId
            : context.RequestId;
        return await executor.ExecuteAsync(new TechnologyComponent(request.TenantId, id),
            component => AggregateOutcome.CommitOnSuccess(component.Record(content, actor,
                clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
