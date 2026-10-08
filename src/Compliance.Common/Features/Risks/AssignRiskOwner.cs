using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>Assigns the governed workforce person accountable for the risk; HTTP-only.</summary>
[Discriminator("bdgrz.risk.owner.assign", 1)]
public sealed record AssignRiskOwner(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long ExpectedRevision, Uuid PersonId, string Rationale)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
