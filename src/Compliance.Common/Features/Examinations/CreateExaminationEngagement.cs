using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the create examination engagement intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.examinations.create-examination-engagement", 1)]
public sealed record CreateExaminationEngagement(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId, string EngagementType, string AuditFirm, DateOnly PeriodStart, DateOnly? PeriodEnd, IReadOnlyList<string> TrustServicesCategories, Uuid BoundaryId) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
