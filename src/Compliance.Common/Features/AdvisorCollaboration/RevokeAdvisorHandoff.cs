using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records the revoke advisor handoff intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.advisor-collaboration.revoke-advisor-handoff", 1)]
public sealed record RevokeAdvisorHandoff(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, string Rationale) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
