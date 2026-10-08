using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     Activates the initial immutable control version. Governed applicability and the control
///     owner's current membership are verified against their sources before execution; the
///     aggregate then binds the decision to the exact reviewed draft revision.
/// </summary>
public sealed class ApproveControlHandler(IAggregateExecutor executor, IAggregateReader reader,
    IControlApplicabilityReferenceValidator applicability,
    ControlActivationReleaseGate releaseGate, ControlLifecycleReleaseGate lifecycleGate,
    ControlImpactService impact, TimeProvider clock)
    : IRequestHandler<ApproveControl>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ApproveControl> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Control decision requires a personal HTTP invocation."));
        if (!releaseGate.IsEnabled)
            return Result.Failure(ControlActivationReleaseGate.Unavailable);
        var request = context.Request;
        var current = await reader.HydrateAsync(new ControlDraft(request.TenantId,
            request.ControlId), ct).ConfigureAwait(false);
        if (!current.IsVisible || current.ProgramId != request.ProgramId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The control draft was not found."));
        if (current.Revision != request.ExpectedRevision)
            return Result.Failure(VersionedRecordRules.StaleRevision("control draft",
                current.Revision).ToRequestError());
        if (current.IsApproved)
        {
            // A successor must be approved against the complete, unchanged impact preview.
            if (!lifecycleGate.IsEnabled)
                return Result.Failure(ControlLifecycleReleaseGate.Unavailable);
            var confirmed = await impact.ConfirmAsync(new PreviewControlImpact(request.TenantId,
                request.ProgramId, request.ControlId, request.ExpectedRevision),
                request.ImpactDigest, ct).ConfigureAwait(false);
            if (!confirmed.IsSuccess)
                return confirmed;
        }
        var validation = await applicability.ValidateAsync(request.TenantId,
            current.CurrentContent, ct).ConfigureAwait(false);
        if (!validation.IsSuccess)
            return validation;
        var now = clock.GetUtcNow();
        var verifiedOwners = new HashSet<Uuid>();
        foreach (var ownerMemberId in current.CurrentOwnerMemberIds(now))
        {
            var member = await reader.HydrateAsync(Member.ForVerification(request.TenantId,
                ownerMemberId), ct).ConfigureAwait(false);
            if (member.IsRegistered && !member.IsSuspended &&
                member.Affiliation == "client_personnel")
                verifiedOwners.Add(ownerMemberId);
        }
        // A designated workforce person is verified against the roster with their current
        // member correlation, which also binds owner-based separation of duties.
        ControlPersonOwner? verifiedPerson = null;
        if (current.CurrentPersonOwner is { } designated)
        {
            var person = await reader.HydrateAsync(new Person(request.TenantId,
                designated.PersonId), ct).ConfigureAwait(false);
            if (person.IsCreated)
                verifiedPerson = new ControlPersonOwner(designated.PersonId,
                    person.CorrelatedUserId is { } correlated
                        ? RbacIds.Member(request.TenantId, correlated)
                        : null, designated.DesignationId);
        }
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
                // Applicability and ownership were verified at this revision; an interleaved
                // content change invalidates them.
                if (control.Revision != request.ExpectedRevision)
                    return AggregateOutcome.Discard(Result.Failure(VersionedRecordRules
                        .StaleRevision("control draft", control.Revision).ToRequestError()));
                return CommandFailureRequestAdapter.ToOutcome(control.Approve(request.ProgramId,
                    request.ExpectedRevision, context.RequestId, request.AcceptedReviewDecisionId,
                    request.EffectiveFrom, request.Rationale, verifiedOwners,
                    RbacIds.Member(request.TenantId, userId),
                    UserIdentityClaims.BdgrzDisplay(context.Actor, userId), now, waiver,
                    request.ImpactDigest, verifiedPerson));
            },
            context, ct).ConfigureAwait(false);
    }
}
