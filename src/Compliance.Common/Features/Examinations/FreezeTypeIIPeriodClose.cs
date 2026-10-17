using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the freeze type iiperiod close intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.examinations.freeze-type-iiperiod-close", 1)]
public sealed record FreezeTypeIIPeriodClose(Uuid TenantId, Uuid ProgramId, Uuid PeriodId, Uuid CloseSnapshotId, long ExpectedRevision) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
