using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records the classify audit population row intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.audit-populations.classify-audit-population-row", 1)]
public sealed record ClassifyAuditPopulationRow(Uuid TenantId, Uuid ProgramId, Uuid PopulationId, Uuid RowId, string Classification, string Rationale) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
