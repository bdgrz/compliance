using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class PreviewWorkforceSourceObservationHandler(IAggregateReader reader,
    WorkforceSourceTargets targets, IPermissionAuthorizer permissions)
    : IRequestHandler<PreviewWorkforceSourceObservation, WorkforceSourcePreview>
{
    public async ValueTask<Result<WorkforceSourcePreview>> HandleAsync(
        IRequestContext<PreviewWorkforceSourceObservation> context, CancellationToken ct)
    {
        var request = context.Request;
        var observation = await reader.HydrateAsync(new WorkforceSourceObservation(request.TenantId,
            request.ObservationId), ct).ConfigureAwait(false);
        if (observation.Observation is not { } observed)
            return Result<WorkforceSourcePreview>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The source observation was not found."));
        var access = await WorkforceSourceComparisonAccess.CheckAsync(permissions, request.TenantId,
            context.Actor, observed.TargetKind == "work_relationship", ct).ConfigureAwait(false);
        if (!access.IsSuccess)
            return Result<WorkforceSourcePreview>.Failure(access.Error);
        var target = await targets.GetAsync(request.TenantId, observed.TargetKind, observed.TargetId, ct)
            .ConfigureAwait(false);
        return target.IsSuccess
            ? Result<WorkforceSourcePreview>.Success(WorkforceSourceReconciliation.Preview(observed,
                observation.Revision, target.Value.Revision, target.Value.Facts, observation.Decision))
            : Result<WorkforceSourcePreview>.Failure(target.Error);
    }
}
