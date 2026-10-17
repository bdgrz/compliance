using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the amend type ibaseline intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.examinations.amend-type-ibaseline", 1)]
public sealed record AmendTypeIBaseline(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId, Uuid BaselineId, long ExpectedRevision, string Reason, IReadOnlyList<Uuid> ChangedRecordIds) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
