using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Records the acting member's own acceptance. The claimed authority must be held through
///     an active program grant; the aggregate then applies M0-D10 appetite and expiry rules.
/// </summary>
public sealed class AcceptRiskHandler(IAggregateExecutor executor, IAggregateReader reader,
    IAccessGrantPermissionAuthorizer permissions, TimeProvider clock)
    : IRequestHandler<AcceptRisk, RiskAcceptanceView>
{
    public async ValueTask<Result<RiskAcceptanceView>> HandleAsync(
        IRequestContext<AcceptRisk> context, CancellationToken ct)
    {
        var request = context.Request;
        var risk = await reader.HydrateAsync(new RiskDraft(request.TenantId, request.RiskId), ct)
            .ConfigureAwait(false);
        if (!risk.IsCreated || risk.ProgramId != request.ProgramId)
            return Result<RiskAcceptanceView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The risk was not found."));
        var current = await reader.HydrateAsync(new RiskEvaluation(request.TenantId,
            request.RiskId), ct).ConfigureAwait(false);
        if (current.FindAssessment(request.ResidualAssessmentId) is not { Phase: RiskEvaluation.Residual } residual)
            return Result<RiskAcceptanceView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Accept the current residual assessment."));
        var methods = await reader.HydrateAsync(new RiskMethod(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        var method = methods.Find(residual.MethodVersionId)
                     ?? throw new InvalidOperationException("A residual assessment must cite a published method.");
        var actor = RiskActor.From(context.Actor, request.TenantId);
        var permission = RbacPermissions.RiskAcceptanceFor(request.ApproverAuthority);
        var held = permission is not null && await permissions.IsAllowedAsync(request.TenantId,
            actor.UserId, actor.MemberId, request.ProgramId, permission, ct).ConfigureAwait(false);
        // The owner's correlated member at assignment and now are both bound by owner SoD.
        var governance = await reader.HydrateAsync(new RiskGovernanceLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        Uuid? ownerMemberId = null;
        if (governance.OwnerOf(request.RiskId) is { } owner)
        {
            var (_, currentMemberId) = await RiskOwnerResolution.ResolveAsync(reader,
                request.TenantId, owner.PersonId, ct).ConfigureAwait(false);
            ownerMemberId = owner.CorrelatedMemberId == actor.MemberId ||
                            currentMemberId == actor.MemberId
                ? actor.MemberId
                : currentMemberId ?? owner.CorrelatedMemberId;
        }
        SeparationOfDutiesWaiver? waiver = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(new RiskEvaluation(request.TenantId, request.RiskId),
            evaluation =>
            {
                var failure = evaluation.Accept(request.ProgramId, request.ExpectedRevision,
                    context.RequestId, request.ResidualAssessmentId, method,
                    request.ApproverAuthority, held, request.ExpiresAt, request.Rationale,
                    actor.MemberId, actor.Display, clock.GetUtcNow(), ownerMemberId, waiver);
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    evaluation.FindAcceptance(context.RequestId)!);
            }, context, ct).ConfigureAwait(false);
    }
}
