using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class GetControlDraftHandler(ControlDraftReadConsistency consistency)
    : IRequestHandler<GetControlDraft, ControlDraftView>
{
    public ValueTask<Result<ControlDraftView>> HandleAsync(
        IRequestContext<GetControlDraft> context, CancellationToken ct) =>
        consistency.GetAsync(context.Request.TenantId, context.Request.ProgramId,
            context.Request.ControlId, context.Request.MinimumRevision, ct);
}
