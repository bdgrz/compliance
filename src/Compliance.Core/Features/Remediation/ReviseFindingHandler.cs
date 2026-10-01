using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Records an attributable severity, ownership, due-date, scope, or root-cause change.</summary>
public sealed class ReviseFindingHandler(IAggregateExecutor executor, IAggregateReader reader,
    TimeProvider clock) : IRequestHandler<ReviseFinding, FindingView>
{
    public async ValueTask<Result<FindingView>> HandleAsync(IRequestContext<ReviseFinding> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!await RemediationCommands.IsActiveMemberAsync(reader, request.TenantId,
                request.OwnerMemberId, ct).ConfigureAwait(false))
            return Result<FindingView>.Failure(RemediationCommands.InactiveOwner());
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var now = clock.GetUtcNow();
        return await RemediationCommands.ExecuteAsync(executor, context, request.TenantId,
            request.ProgramId, request.FindingId, now, ledger => ledger.Revise(request.FindingId,
                request.ExpectedRevision, request.Severity, request.OwnerMemberId, request.DueOn,
                request.AffectedScope, request.RootCause, request.Reason,
                ActorReference.ForMember(actor.MemberId, actor.Display), now), ct)
            .ConfigureAwait(false);
    }
}
