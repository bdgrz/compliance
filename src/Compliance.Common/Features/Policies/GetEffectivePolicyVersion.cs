using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>The approved version whose effective interval contains the date.</summary>
[Discriminator("bdgrz.policy.version.effective.get", 1)]
public sealed record GetEffectivePolicyVersion(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    DateOnly EffectiveOn)
    : IRequest<PolicyVersionView>, IProgramReadRequest, ICallable;
