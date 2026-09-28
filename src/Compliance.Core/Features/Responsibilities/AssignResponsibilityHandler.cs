using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

public sealed class AssignResponsibilityHandler(IAggregateExecutor executor,
    IAggregateReader reader, ITenantMembershipDirectoryReader memberships,
    IResponsibilityScopeValidator scopeValidator, TimeProvider clock)
    : IRequestHandler<AssignResponsibility>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<AssignResponsibility> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var scopeResult = await scopeValidator.ValidateAsync(request.TenantId, request.Scope, ct)
            .ConfigureAwait(false);
        if (!scopeResult.IsSuccess)
            return scopeResult;
        var member = await memberships.GetAsync(request.TenantId.ToString(), request.MemberUserId, ct)
            .ConfigureAwait(false);
        if (member is null || member.Affiliation == "firm_staff")
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The responsibility assignee must be an active tenant member."));
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var actorUserId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Responsibility management requires a Bdgrz user identity."));
        var actorMemberId = RbacIds.Member(request.TenantId, actorUserId);
        var actorDisplay = UserIdentityClaims.BdgrzDisplay(context.Actor, actorUserId);
        var waivers = new List<SeparationOfDutiesWaiver>();
        foreach (var waiverId in request.SeparationOfDutiesWaiverIds ?? [])
            waivers.Add(await reader.HydrateAsync(new SeparationOfDutiesWaiver(
                request.TenantId, waiverId), ct).ConfigureAwait(false));
        return await executor.ExecuteAsync(new SystemBoundary(request.TenantId, request.Scope.RecordId), boundary =>
        {
            var failure = boundary.AssignResponsibility(request.Scope, context.RequestId,
                RbacIds.Member(request.TenantId, request.MemberUserId), request.Type,
                actorMemberId, actorDisplay, clock.GetUtcNow(),
                request.EffectiveFrom, request.EffectiveUntil, waivers);
            return CommandFailureRequestAdapter.ToOutcome(failure);
        }, context, ct).ConfigureAwait(false);
    }
}
