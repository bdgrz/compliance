using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>
///     An independent review of the exact pending draft or retirement revision. HTTP-only: it is
///     deliberately not an MCP tool.
/// </summary>
[Discriminator("bdgrz.policy.review", 1)]
public sealed record ReviewPolicyDraft(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long ExpectedRevision, string Outcome, string Rationale,
    Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<PolicyDecisionView>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
