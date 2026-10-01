using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public sealed class RevisePolicyDraftHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<RevisePolicyDraft, PolicyRegistration>
{
    public ValueTask<Result<PolicyRegistration>> HandleAsync(
        IRequestContext<RevisePolicyDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = PolicyActor.From(context, request.TenantId);
        return executor.ExecuteAsync(new Policy(request.TenantId, request.PolicyId),
            policy => PolicySource.Registration(policy, policy.Revise(request.ProgramId,
                request.ExpectedRevision, request.Content, actor.Reference, actor.MemberId,
                clock.GetUtcNow())),
            context, ct);
    }
}
