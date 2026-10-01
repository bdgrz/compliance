using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public sealed class ProposePolicyRetirementHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<ProposePolicyRetirement, PolicyRegistration>
{
    public ValueTask<Result<PolicyRegistration>> HandleAsync(
        IRequestContext<ProposePolicyRetirement> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = PolicyActor.From(context, request.TenantId);
        return executor.ExecuteAsync(new Policy(request.TenantId, request.PolicyId),
            policy => PolicySource.Registration(policy, policy.ProposeRetirement(
                request.ProgramId, request.ExpectedVersion, request.EffectiveUntil,
                request.Rationale, actor.Reference, actor.MemberId, clock.GetUtcNow())),
            context, ct);
    }
}
