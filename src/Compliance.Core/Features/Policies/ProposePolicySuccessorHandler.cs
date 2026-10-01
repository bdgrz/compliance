using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public sealed class ProposePolicySuccessorHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<ProposePolicySuccessor, PolicyRegistration>
{
    public ValueTask<Result<PolicyRegistration>> HandleAsync(
        IRequestContext<ProposePolicySuccessor> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = PolicyActor.From(context, request.TenantId);
        return executor.ExecuteAsync(new Policy(request.TenantId, request.PolicyId),
            policy => PolicySource.Registration(policy, policy.ProposeSuccessor(
                request.ProgramId, request.ExpectedVersion, request.Content, actor.Reference,
                actor.MemberId, clock.GetUtcNow())),
            context, ct);
    }
}
