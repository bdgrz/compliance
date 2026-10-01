using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>Opens a successor draft from the exact current approved version.</summary>
[Discriminator("bdgrz.policy.successor.propose", 1)]
public sealed record ProposePolicySuccessor(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long ExpectedVersion, PolicyContent Content)
    : IRequest<PolicyRegistration>, IProgramScopedRequest, ICallable;
