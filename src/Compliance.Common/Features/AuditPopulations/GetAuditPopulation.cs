using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.audit-populations.get-audit-population", 1)]
public sealed record GetAuditPopulation(Uuid TenantId, Uuid ProgramId, Uuid AuditPopulationId)
    : IRequest<AuditPopulationView>, IProgramReadRequest, ICallable;
