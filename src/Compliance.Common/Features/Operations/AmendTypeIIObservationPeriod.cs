using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Records the amend type iiobservation period intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.operations.amend-type-iiobservation-period", 1)]
public sealed record AmendTypeIIObservationPeriod(Uuid TenantId, Uuid ProgramId, Uuid PeriodId, long ExpectedRevision, DateOnly? PeriodStart, DateOnly? PeriodEnd, string AmendmentReason) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
