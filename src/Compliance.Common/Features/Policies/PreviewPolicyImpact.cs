using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

[Discriminator("bdgrz.policy.impact.preview", 1)]
public sealed record PreviewPolicyImpact(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long ExpectedRevision)
    : IRequest<PolicyImpactPreview>, IProgramReadRequest, ICallable;
