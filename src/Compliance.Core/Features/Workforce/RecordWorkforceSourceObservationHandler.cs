using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class RecordWorkforceSourceObservationHandler(IAggregateExecutor executor,
    IAggregateReader reader, WorkforceSourceTargets targets, TimeProvider clock, IPermissionAuthorizer permissions)
    : IRequestHandler<RecordWorkforceSourceObservation, WorkforceSourceRegistration>
{
    public async ValueTask<Result<WorkforceSourceRegistration>> HandleAsync(
        IRequestContext<RecordWorkforceSourceObservation> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Source is null)
            return Result<WorkforceSourceRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "A source identity is required."));
        var id = WorkforceSourceObservation.IdFor(request.TenantId, request.Source);
        var existing = await reader.HydrateAsync(new WorkforceSourceObservation(request.TenantId, id), ct)
            .ConfigureAwait(false);
        var access = await WorkforceSourceComparisonAccess.CheckAsync(permissions, request.TenantId,
            context.Actor, request.TargetKind == "work_relationship" || request.Facts is { WorkRelationship: not null } ||
                           existing.Observation?.TargetKind == "work_relationship", ct).ConfigureAwait(false);
        if (!access.IsSuccess)
            return Result<WorkforceSourceRegistration>.Failure(access.Error);
        if (!existing.IsRecorded)
        {
            var target = await targets.GetAsync(request.TenantId, request.TargetKind, request.TargetId, ct)
                .ConfigureAwait(false);
            if (!target.IsSuccess)
                return Result<WorkforceSourceRegistration>.Failure(target.Error);
            if (request.ExpectedTargetRevision != target.Value.Revision)
                return Result<WorkforceSourceRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The canonical workforce revision changed; preview the current record before observing it."));
        }
        return await executor.ExecuteAsync(new WorkforceSourceObservation(request.TenantId, id),
            observation => AggregateOutcome.CommitOnSuccess(observation.Record(request.Source,
                request.TargetKind, request.TargetId, request.ExpectedTargetRevision, request.Facts,
                request.ObservedAt, WorkforceActor.From(context), clock.GetUtcNow())), context, ct)
            .ConfigureAwait(false);
    }
}
