using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     Approves a pending retirement over HTTP only. The complete impact preview is recomputed
///     and must match the acknowledged digest before the aggregate binds the decision to the
///     exact reviewed retirement revision.
/// </summary>
public sealed class RetireControlHandler(IAggregateExecutor executor, IAggregateReader reader,
    ControlImpactService impact, ControlLifecycleReleaseGate releaseGate, TimeProvider clock)
    : IRequestHandler<RetireControl>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RetireControl> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Control decision requires a personal HTTP invocation."));
        if (!releaseGate.IsEnabled)
            return Result.Failure(ControlLifecycleReleaseGate.Unavailable);
        var request = context.Request;
        var confirmed = await impact.ConfirmAsync(new PreviewControlImpact(request.TenantId,
            request.ProgramId, request.ControlId, request.ExpectedRevision), request.ImpactDigest,
            ct).ConfigureAwait(false);
        if (!confirmed.IsSuccess)
            return confirmed;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        SeparationOfDutiesWaiver? waiver = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(new ControlDraft(request.TenantId, request.ControlId),
            control =>
            {
                // The preview was confirmed at this revision; an interleaved change invalidates it.
                if (control.Revision != request.ExpectedRevision)
                    return AggregateOutcome.Discard(Result.Failure(VersionedRecordRules
                        .StaleRevision("control draft", control.Revision).ToRequestError()));
                return CommandFailureRequestAdapter.ToOutcome(control.Retire(request.ProgramId,
                    request.ExpectedRevision, context.RequestId, request.AcceptedReviewDecisionId,
                    request.ImpactDigest, request.Rationale,
                    RbacIds.Member(request.TenantId, userId),
                    UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow(),
                    waiver));
            },
            context, ct).ConfigureAwait(false);
    }
}
