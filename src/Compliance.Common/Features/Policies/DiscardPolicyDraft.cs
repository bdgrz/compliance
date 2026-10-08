using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>Deletes a never-approved, never-reviewed draft with no retained relationships.</summary>
[Discriminator("bdgrz.policy.draft.discard", 1)]
public sealed record DiscardPolicyDraft(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long ExpectedRevision, string Rationale)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
