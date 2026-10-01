using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Opens an evidence request for an active client member, optionally tied to a control in the program.</summary>
public sealed class OpenEvidenceRequestHandler(IAggregateExecutor executor, IAggregateReader reader,
    TimeProvider clock) : IRequestHandler<OpenEvidenceRequest, EvidenceRequestView>
{
    public async ValueTask<Result<EvidenceRequestView>> HandleAsync(IRequestContext<OpenEvidenceRequest> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!await RemediationCommands.IsActiveMemberAsync(reader, request.TenantId, request.OwnerMemberId, ct)
                .ConfigureAwait(false))
            return Result<EvidenceRequestView>.Failure(RemediationCommands.InactiveOwner());
        if (request.ControlId is { } controlId)
        {
            var control = await reader.HydrateAsync(new ControlDraft(request.TenantId, controlId), ct)
                .ConfigureAwait(false);
            if (!control.IsCreated || control.ProgramId != request.ProgramId)
                return Result<EvidenceRequestView>.Failure(new RequestError(RequestErrorKind.Validation,
                    "The control was not found in this program."));
        }
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        return await EvidenceRequestCommands.ExecuteAsync(executor, context, request.TenantId, request.ProgramId,
            context.RequestId, ledger => ledger.OpenRequest(context.RequestId, request.Title, request.Instructions,
                request.OwnerMemberId, request.DueOn, request.ControlId,
                ActorReference.ForMember(actor.MemberId, actor.Display), clock.GetUtcNow()), ct).ConfigureAwait(false);
    }
}
