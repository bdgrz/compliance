using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records the revise audit population definition intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.audit-populations.revise-audit-population-definition", 1)]
public sealed record ReviseAuditPopulationDefinition(Uuid TenantId, Uuid ProgramId, Uuid DefinitionId, long ExpectedRevision, string SourceBoundary, string InclusionRules, string ExclusionRules, string ChangeReason) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
