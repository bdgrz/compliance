using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Controls;
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
        if (member is null || member.IsSuspended || member.Affiliation == "firm_staff")
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
        // Membership is a projection. Read the source member after waiver hydration so a
        // suspension committed during preparation stops this assignment before execution.
        var currentMember = await reader.HydrateAsync(new Member(request.TenantId, request.MemberUserId), ct)
            .ConfigureAwait(false);
        if (!currentMember.IsRegistered || currentMember.IsSuspended ||
            currentMember.Affiliation == "firm_staff")
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The responsibility assignee must be an active tenant member."));
        var memberId = RbacIds.Member(request.TenantId, request.MemberUserId);
        var assignedAt = clock.GetUtcNow();
        // Responsibilities live on the aggregate that owns the exact record version, so the
        // assignment and the record's revision share one optimistic concurrency boundary.
        if (request.RecordType == SeparationOfDutiesRecordTypes.Commitment)
            return await executor.ExecuteAsync(new CommitmentDraft(request.TenantId,
                request.Scope.RecordId), commitment => CommandFailureRequestAdapter.ToOutcome(
                commitment.AssignResponsibility(request.Scope, context.RequestId, memberId,
                    request.Type, actorMemberId, actorDisplay, assignedAt,
                    request.EffectiveFrom, request.EffectiveUntil, waivers)),
                context, ct).ConfigureAwait(false);
        if (request.RecordType == SeparationOfDutiesRecordTypes.Control)
            return await executor.ExecuteAsync(new ControlDraft(request.TenantId,
                request.Scope.RecordId), control => CommandFailureRequestAdapter.ToOutcome(
                control.AssignResponsibility(request.Scope, context.RequestId, memberId,
                    request.Type, actorMemberId, actorDisplay, assignedAt,
                    request.EffectiveFrom, request.EffectiveUntil, waivers)),
                context, ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(new SystemBoundary(request.TenantId, request.Scope.RecordId), boundary =>
        {
            var failure = boundary.AssignResponsibility(request.Scope, context.RequestId,
                memberId, request.Type, actorMemberId, actorDisplay, assignedAt,
                request.EffectiveFrom, request.EffectiveUntil, waivers);
            return CommandFailureRequestAdapter.ToOutcome(failure);
        }, context, ct).ConfigureAwait(false);
    }
}
