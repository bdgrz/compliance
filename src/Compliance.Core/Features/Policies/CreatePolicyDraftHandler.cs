using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public sealed class CreatePolicyDraftHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<CreatePolicyDraft, PolicyRegistration>
{
    public ValueTask<Result<PolicyRegistration>> HandleAsync(
        IRequestContext<CreatePolicyDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = PolicyActor.From(context, request.TenantId);
        var policyId = Policy.IdFor(request.TenantId, request.ProgramId, request.Identifier);
        return executor.ExecuteAsync(new Policy(request.TenantId, policyId),
            policy => AggregateOutcome.CommitOnSuccess(policy.Create(request.ProgramId,
                context.RequestId, request.Identifier, request.Content, actor.Reference,
                actor.MemberId, clock.GetUtcNow())),
            context, ct);
    }
}
