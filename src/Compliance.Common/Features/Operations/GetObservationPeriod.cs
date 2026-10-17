using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.operations.get-observation-period", 1)]
public sealed record GetObservationPeriod(Uuid TenantId, Uuid ProgramId, Uuid ObservationPeriodId)
    : IRequest<ObservationPeriodView>, IProgramReadRequest, ICallable;
