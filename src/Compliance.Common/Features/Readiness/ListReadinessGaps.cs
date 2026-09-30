using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Lists an assessment's gaps with their current plan; plan_state is planned or unplanned.</summary>
[Discriminator("bdgrz.readiness.gaps.list", 1)]
public sealed record ListReadinessGaps(Uuid TenantId, Uuid ProgramId, Uuid AssessmentId,
    string? PlanState = null, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ReadinessGapView>>, IProgramReadRequest, ICallable;
