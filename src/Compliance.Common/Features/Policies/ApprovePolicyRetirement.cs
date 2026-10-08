using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>Approves the reviewed pending retirement. HTTP-only personal approval.</summary>
[Discriminator("bdgrz.policy.retire", 1)]
public sealed record ApprovePolicyRetirement(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long ExpectedRevision, Uuid AcceptedReviewDecisionId, string Rationale,
    Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<PolicyDecisionView>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
