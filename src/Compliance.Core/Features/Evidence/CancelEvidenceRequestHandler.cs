using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

public sealed class CancelEvidenceRequestHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<CancelEvidenceRequest, EvidenceRequestView>
{
    public ValueTask<Result<EvidenceRequestView>> HandleAsync(IRequestContext<CancelEvidenceRequest> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        return EvidenceRequestCommands.ExecuteAsync(executor, context, request.TenantId, request.ProgramId,
            request.EvidenceRequestId, ledger => ledger.Cancel(request.EvidenceRequestId, request.ExpectedRevision,
                request.Rationale, ActorReference.ForMember(actor.MemberId, actor.Display), clock.GetUtcNow()), ct);
    }
}
