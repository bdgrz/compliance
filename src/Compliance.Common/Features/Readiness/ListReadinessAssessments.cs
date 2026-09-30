using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

[Discriminator("bdgrz.readiness.assessments.list", 1)]
public sealed record ListReadinessAssessments(Uuid TenantId, Uuid ProgramId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<ReadinessAssessmentSummaryView>>, IProgramReadRequest, ICallable;
