using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

public sealed class RevokeResponsibilityHandler(IAggregateExecutor executor,
    IResponsibilityScopeValidator scopeValidator, TimeProvider clock)
    : IRequestHandler<RevokeResponsibility>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RevokeResponsibility> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var scopeResult = await scopeValidator.ValidateAsync(request.TenantId, request.Scope, ct)
            .ConfigureAwait(false);
        if (!scopeResult.IsSuccess)
            return scopeResult;
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var actorUserId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Responsibility management requires a Bdgrz user identity."));
        var actorMemberId = RbacIds.Member(request.TenantId, actorUserId);
        if (request.RecordType == SeparationOfDutiesRecordTypes.Control)
            return await executor.ExecuteAsync(new ControlDraft(request.TenantId,
                request.Scope.RecordId), control => CommandFailureRequestAdapter.ToOutcome(
                control.RevokeResponsibility(request.Scope, request.AssignmentId, actorMemberId,
                    UserIdentityClaims.BdgrzDisplay(context.Actor, actorUserId),
                    clock.GetUtcNow(), request.Reason)),
                context, ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(new SystemBoundary(request.TenantId, request.Scope.RecordId), boundary =>
        {
            var failure = boundary.RevokeResponsibility(request.Scope, request.AssignmentId,
                actorMemberId, UserIdentityClaims.BdgrzDisplay(context.Actor, actorUserId),
                clock.GetUtcNow(), request.Reason);
            return CommandFailureRequestAdapter.ToOutcome(failure);
        }, context, ct).ConfigureAwait(false);
    }
}
