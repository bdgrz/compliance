using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     Designates a recorded workforce person as owner of the exact pending control revision.
///     The person's correlated member is snapshotted for owner-based separation of duties.
/// </summary>
public sealed class DesignateControlOwnerPersonHandler(IAggregateExecutor executor,
    IAggregateReader reader, ControlLifecycleReleaseGate releaseGate, TimeProvider clock)
    : IRequestHandler<DesignateControlOwnerPerson>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<DesignateControlOwnerPerson> context, CancellationToken ct)
    {
        if (!releaseGate.IsEnabled)
            return Result.Failure(ControlLifecycleReleaseGate.Unavailable);
        var request = context.Request;
        Uuid? correlatedMemberId = null;
        if (request.PersonId is { } personId)
        {
            var person = await reader.HydrateAsync(new Person(request.TenantId, personId), ct)
                .ConfigureAwait(false);
            if (!person.IsCreated)
                return Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "The control owner must be a recorded workforce person in this organization."));
            correlatedMemberId = person.CorrelatedUserId is { } userId
                ? RbacIds.Member(request.TenantId, userId)
                : null;
        }
        var actorUserId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var actorMemberId = RbacIds.Member(request.TenantId, actorUserId);
        var actor = ActorReference.ForMember(actorMemberId,
            UserIdentityClaims.BdgrzDisplay(context.Actor, actorUserId));
        return await executor.ExecuteAsync(new ControlDraft(request.TenantId, request.ControlId),
            control => CommandFailureRequestAdapter.ToOutcome(control.DesignatePersonOwner(
                request.ProgramId, request.ExpectedRevision, context.RequestId, request.PersonId,
                correlatedMemberId, request.Rationale, actorMemberId, actor, clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
