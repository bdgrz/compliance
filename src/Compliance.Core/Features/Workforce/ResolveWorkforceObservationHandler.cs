using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     Closes an observation that currently exists for the tenant: a projected joiner, mover, or
///     leaver, or a reconciliation finding evaluated now. An already-closed observation may be
///     retried with the same closure even after its facts changed. Closure never changes access.
/// </summary>
public sealed class ResolveWorkforceObservationHandler(IAggregateExecutor executor,
    IAggregateReader reader, IWorkforceObservationDirectoryReader observations,
    WorkforceRosterReconciler reconciler, TimeProvider clock)
    : IRequestHandler<ResolveWorkforceObservation>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ResolveWorkforceObservation> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Workforce decision requires a personal HTTP invocation."));
        var actor = WorkforceActor.From(context);
        var request = context.Request;
        var existing = await reader.HydrateAsync(
            new WorkforceObservationResolution(request.TenantId, request.ObservationId), ct)
            .ConfigureAwait(false);
        if (!existing.IsResolved)
        {
            var stored = await observations.GetAsync(request.TenantId, request.ObservationId, ct)
                .ConfigureAwait(false);
            if (stored is null || stored.TenantId != request.TenantId)
            {
                var evaluated = await reconciler.EvaluateAsync(request.TenantId, ct)
                    .ConfigureAwait(false);
                if (!evaluated.IsSuccess)
                    return Result.Failure(evaluated.Error);
                if (evaluated.Value.All(item => item.ObservationId != request.ObservationId))
                    return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                        "The workforce observation was not found."));
            }
        }
        return await executor.ExecuteAsync(
            new WorkforceObservationResolution(request.TenantId, request.ObservationId),
            resolution => CommandFailureRequestAdapter.ToOutcome(resolution.Resolve(
                request.Resolution, request.Note, actor, clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
