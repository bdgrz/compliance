using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the freeze type ibaseline intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.examinations.freeze-type-ibaseline", 1)]
public sealed record FreezeTypeIBaseline(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId, Uuid BaselineId, long ExpectedRevision, string ConfirmationRationale) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
