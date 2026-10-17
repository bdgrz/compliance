using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Records the start type iiobservation period intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.operations.start-type-iiobservation-period", 1)]
public sealed record StartTypeIIObservationPeriod(Uuid TenantId, Uuid ProgramId, Uuid PeriodId, Uuid ExaminationId, Uuid PlanId, DateOnly PeriodStart, DateOnly PeriodEnd) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
