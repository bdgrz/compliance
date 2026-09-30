using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class ProposeControlSuccessorHandler(IAggregateExecutor executor,
    IControlApplicabilityReferenceValidator applicability,
    ControlLifecycleReleaseGate releaseGate, TimeProvider clock)
    : IRequestHandler<ProposeControlSuccessor, ControlSuccessorRegistration>
{
    public async ValueTask<Result<ControlSuccessorRegistration>> HandleAsync(
        IRequestContext<ProposeControlSuccessor> context, CancellationToken ct)
    {
        if (!releaseGate.IsEnabled)
            return Result<ControlSuccessorRegistration>.Failure(
                ControlLifecycleReleaseGate.Unavailable);
        var request = context.Request;
        if (ControlDraft.ValidateContent(request.Content) is { } structural)
            return Result<ControlSuccessorRegistration>.Failure(structural);
        var validation = await applicability.ValidateAsync(request.TenantId, request.Content, ct)
            .ConfigureAwait(false);
        if (!validation.IsSuccess)
            return Result<ControlSuccessorRegistration>.Failure(validation.Error);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return await executor.ExecuteAsync(new ControlDraft(request.TenantId, request.ControlId),
            control =>
            {
                var failure = control.ProposeSuccessor(request.ProgramId,
                    request.ExpectedApprovedVersionId, request.Content,
                    RbacIds.Member(request.TenantId, userId),
                    UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow());
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    new ControlSuccessorRegistration(request.ControlId, control.DraftVersionId,
                        request.ExpectedApprovedVersionId, control.Revision));
            },
            context, ct).ConfigureAwait(false);
    }
}
