using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records the define audit population intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.audit-populations.define-audit-population", 1)]
public sealed record DefineAuditPopulation(Uuid TenantId, Uuid ProgramId, Uuid DefinitionId, Uuid PeriodCloseId, string Name, string SourceBoundary, DateOnly PeriodStart, DateOnly PeriodEnd) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
