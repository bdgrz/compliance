using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Records the acting member's own verification and closure decision; HTTP-only.</summary>
public sealed class CloseFindingHandler(IAggregateExecutor executor, IAggregateReader reader,
    TimeProvider clock) : IRequestHandler<CloseFinding, FindingView>
{
    public async ValueTask<Result<FindingView>> HandleAsync(IRequestContext<CloseFinding> context,
        CancellationToken ct)
    {
        var request = context.Request;
        SeparationOfDutiesWaiver? waiver = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var now = clock.GetUtcNow();
        return await RemediationCommands.ExecuteAsync(executor, context, request.TenantId,
            request.ProgramId, request.FindingId, now, ledger => ledger.Close(request.FindingId,
                request.ExpectedRevision, context.RequestId, request.VerificationRationale,
                request.ResolutionEvidence ?? [], request.Rationale, actor.MemberId,
                ActorReference.ForMember(actor.MemberId, actor.Display), now, waiver), ct)
            .ConfigureAwait(false);
    }
}
