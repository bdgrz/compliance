using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the amend type iiperiod close intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.examinations.amend-type-iiperiod-close", 1)]
public sealed record AmendTypeIIPeriodClose(Uuid TenantId, Uuid ProgramId, Uuid PeriodId, Uuid CloseSnapshotId, long ExpectedRevision, string AmendmentReason) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
