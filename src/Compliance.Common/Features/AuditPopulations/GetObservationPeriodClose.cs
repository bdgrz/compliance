using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.audit-populations.get-observation-period-close", 1)]
public sealed record GetObservationPeriodClose(Uuid TenantId, Uuid ProgramId, Uuid ObservationPeriodCloseId)
    : IRequest<ObservationPeriodCloseView>, IProgramReadRequest, ICallable;
