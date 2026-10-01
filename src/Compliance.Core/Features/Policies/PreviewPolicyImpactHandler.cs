using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public sealed class PreviewPolicyImpactHandler(PolicyImpactService impact)
    : IRequestHandler<PreviewPolicyImpact, PolicyImpactPreview>
{
    public ValueTask<Result<PolicyImpactPreview>> HandleAsync(
        IRequestContext<PreviewPolicyImpact> context, CancellationToken ct)
    {
        var request = context.Request;
        return impact.PreviewAsync(request.TenantId, request.ProgramId, request.PolicyId,
            request.ExpectedRevision, ct);
    }
}
