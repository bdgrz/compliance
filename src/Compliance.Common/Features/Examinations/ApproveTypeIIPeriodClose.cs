using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the approve type iiperiod close intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.examinations.approve-type-iiperiod-close", 1)]
public sealed record ApproveTypeIIPeriodClose(Uuid TenantId, Uuid ProgramId, Uuid PeriodId, long ExpectedRevision, string Rationale) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
