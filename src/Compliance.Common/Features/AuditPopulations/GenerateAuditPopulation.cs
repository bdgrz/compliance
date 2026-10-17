using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records the generate audit population intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.audit-populations.generate-audit-population", 1)]
public sealed record GenerateAuditPopulation(Uuid TenantId, Uuid ProgramId, Uuid DefinitionId, Uuid PeriodCloseId, string GenerationKey) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
