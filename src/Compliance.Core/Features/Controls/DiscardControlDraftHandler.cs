using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class DiscardControlDraftHandler(IAggregateExecutor executor,
    ControlDraftDiscardReleaseGate releaseGate, TimeProvider clock)
    : IRequestHandler<DiscardControlDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<DiscardControlDraft> context,
        CancellationToken ct)
    {
        if (!releaseGate.IsEnabled)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Control draft discard is unavailable until all active event readers are upgraded.")));
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return executor.ExecuteAsync(new ControlDraft(request.TenantId, request.ControlId),
            control => AggregateOutcome.CommitOnSuccess(control.Discard(request.ProgramId,
                request.ExpectedRevision, request.Rationale,
                RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow())),
            context, ct);
    }
}
