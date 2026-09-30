using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class ProposeControlRetirementHandler(IAggregateExecutor executor,
    ControlLifecycleReleaseGate releaseGate, TimeProvider clock)
    : IRequestHandler<ProposeControlRetirement, ControlRetirementRegistration>
{
    public ValueTask<Result<ControlRetirementRegistration>> HandleAsync(
        IRequestContext<ProposeControlRetirement> context, CancellationToken ct)
    {
        if (!releaseGate.IsEnabled)
            return ValueTask.FromResult(Result<ControlRetirementRegistration>.Failure(
                ControlLifecycleReleaseGate.Unavailable));
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return executor.ExecuteAsync(new ControlDraft(request.TenantId, request.ControlId),
            control =>
            {
                var failure = control.ProposeRetirement(request.ProgramId,
                    request.ExpectedApprovedVersionId, request.EffectiveUntil, request.Rationale,
                    RbacIds.Member(request.TenantId, userId),
                    UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow());
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    new ControlRetirementRegistration(request.ControlId,
                        control.PendingRetirementId ?? Uuid.Empty,
                        request.ExpectedApprovedVersionId, control.Revision));
            },
            context, ct);
    }
}
