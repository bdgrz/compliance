using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.audit-populations.preview-observation-period-close", 1)]
public sealed record PreviewObservationPeriodClose(Uuid TenantId, Uuid ProgramId, Uuid ObservationPeriodId)
    : IRequest<ObservationPeriodClosePreview>, IProgramReadRequest, ICallable;
