using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Records completed corrective work by the action owner or a program manager.</summary>
public sealed class CompleteCorrectiveActionHandler(IAggregateExecutor executor,
    IAggregateReader reader, OperatingAuthority authority, TimeProvider clock)
    : IRequestHandler<CompleteCorrectiveAction, FindingView>
{
    public async ValueTask<Result<FindingView>> HandleAsync(
        IRequestContext<CompleteCorrectiveAction> context, CancellationToken ct)
    {
        var request = context.Request;
        var now = clock.GetUtcNow();
        var current = await reader.HydrateAsync(new RemediationLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (current.Read(request.FindingId, now) is not { } finding)
            return Result<FindingView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The finding was not found."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var action = finding.CorrectiveActions.FirstOrDefault(item =>
            item.ActionId == request.ActionId);
        if (action?.OwnerMemberId != actor.MemberId && !await authority.ManagesProgramAsync(
                request.TenantId, actor, request.ProgramId, ct).ConfigureAwait(false))
            return Result<FindingView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the corrective action owner or a program manager may complete it."));
        return await RemediationCommands.ExecuteAsync(executor, context, request.TenantId,
            request.ProgramId, request.FindingId, now, ledger => ledger.CompleteAction(
                request.FindingId, request.ExpectedRevision, request.ActionId,
                request.ResolutionNotes, request.Evidence ?? [], actor.MemberId,
                ActorReference.ForMember(actor.MemberId, actor.Display), now), ct)
            .ConfigureAwait(false);
    }
}
