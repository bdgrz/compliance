using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public sealed class DiscardPolicyDraftHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<DiscardPolicyDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<DiscardPolicyDraft> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var actor = PolicyActor.From(context, request.TenantId);
        return executor.ExecuteAsync(new Policy(request.TenantId, request.PolicyId),
            policy => CommandFailureRequestAdapter.ToOutcome(policy.Discard(request.ProgramId,
                request.ExpectedRevision, request.Rationale, actor.Reference,
                clock.GetUtcNow())),
            context, ct);
    }
}
