using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

[Discriminator("bdgrz.policy.draft.revise", 1)]
public sealed record RevisePolicyDraft(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long ExpectedRevision, PolicyContent Content)
    : IRequest<PolicyRegistration>, IProgramScopedRequest, ICallable;
