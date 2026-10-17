using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the record management examination review intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.examinations.record-management-examination-review", 1)]
public sealed record RecordManagementExaminationReview(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId, long PackageVersion, string Outcome, string Comments) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
