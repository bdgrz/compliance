using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

[Discriminator("bdgrz.policy.draft.create", 1)]
public sealed record CreatePolicyDraft(Uuid TenantId, Uuid ProgramId, string Identifier,
    PolicyContent Content)
    : IRequest<PolicyRegistration>, IProgramScopedRequest, ICallable;
