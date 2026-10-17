using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the record examination outcome intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.examinations.record-examination-outcome", 1)]
public sealed record RecordExaminationOutcome(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId, string Outcome, string IssuedReportReference, Uuid? ReportArtifactId, DateOnly IssuedOn) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
