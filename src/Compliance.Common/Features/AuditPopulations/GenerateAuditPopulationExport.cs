using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records the generate audit population export intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.audit-populations.generate-audit-population-export", 1)]
public sealed record GenerateAuditPopulationExport(Uuid TenantId, Uuid ProgramId, Uuid PopulationId, long Version, string Format, string ExportKey) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
