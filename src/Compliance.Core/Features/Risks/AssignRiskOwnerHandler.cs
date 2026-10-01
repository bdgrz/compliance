using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Assigns a governed workforce person as risk owner. The person need not sign in; a
///     correlated member is snapshotted for owner-based separation of duties only.
/// </summary>
public sealed class AssignRiskOwnerHandler(IAggregateExecutor executor, IAggregateReader reader,
    TimeProvider clock) : IRequestHandler<AssignRiskOwner>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<AssignRiskOwner> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId,
            request.ProgramId, request.RiskId, ct).ConfigureAwait(false);
        if (!risk.IsSuccess)
            return risk;
        var (exists, memberId) = await RiskOwnerResolution.ResolveAsync(reader,
            request.TenantId, request.PersonId, ct).ConfigureAwait(false);
        if (!exists)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The risk owner must be a recorded workforce person in this organization."));
        var actor = RiskActor.From(context.Actor, request.TenantId);
        return await executor.ExecuteAsync(new RiskGovernanceLedger(request.TenantId,
                request.ProgramId),
            ledger => CommandFailureRequestAdapter.ToOutcome(ledger.AssignOwner(request.RiskId,
                request.ExpectedRevision, request.PersonId, memberId, request.Rationale,
                ActorReference.ForMember(actor.MemberId, actor.Display), clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
