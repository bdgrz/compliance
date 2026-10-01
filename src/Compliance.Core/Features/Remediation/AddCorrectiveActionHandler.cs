using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Adds owned corrective work to a finding; it enters the owner's work queue.</summary>
public sealed class AddCorrectiveActionHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<AddCorrectiveAction, FindingView>
{
    public async ValueTask<Result<FindingView>> HandleAsync(
        IRequestContext<AddCorrectiveAction> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!await RemediationCommands.IsActiveMemberAsync(reader, request.TenantId,
                request.OwnerMemberId, ct).ConfigureAwait(false))
            return Result<FindingView>.Failure(RemediationCommands.InactiveOwner());
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var now = clock.GetUtcNow();
        return await RemediationCommands.ExecuteAsync(executor, context, request.TenantId,
            request.ProgramId, request.FindingId, now, ledger => ledger.AddAction(
                request.FindingId, request.ExpectedRevision, context.RequestId,
                request.Description, request.OwnerMemberId, request.DueOn,
                ActorReference.ForMember(actor.MemberId, actor.Display), now), ct)
            .ConfigureAwait(false);
    }
}
