using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class GetDataFlowHandler(IAggregateReader aggregates,
    TechnologyInventoryReadConsistency consistency,
    TechnologyInventoryRestrictedVisibility visibility)
    : IRequestHandler<GetDataFlow, DataFlowView>
{
    public async ValueTask<Result<DataFlowView>> HandleAsync(
        IRequestContext<GetDataFlow> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.MinimumRevision is < 1)
            return Result<DataFlowView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum data flow revision must be positive."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        var source = await aggregates.HydrateAsync(new DataFlow(request.TenantId,
            request.DataFlowId), ct).ConfigureAwait(false);
        if (!source.IsCreated || !await visibility.CanReadFlowAsync(request.TenantId, userId,
                source.Id, source.Content?.Classification, ct).ConfigureAwait(false))
            return Result<DataFlowView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The data flow was not found."));
        var flow = await consistency.GetFlowAsync(request.TenantId, request.DataFlowId,
            request.MinimumRevision, ct).ConfigureAwait(false);
        if (!flow.IsSuccess)
            return flow;
        if (!await visibility.CanReadFlowAsync(request.TenantId, userId, flow.Value, ct)
                .ConfigureAwait(false))
            return Result<DataFlowView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The data flow was not found."));
        return flow;
    }
}
