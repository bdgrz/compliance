using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     Lists an assessment's gaps with their current plan, filtered by plan_state (planned or
///     unplanned), owner, kind, rule, or subject (criterion or source family).
/// </summary>
[Discriminator("bdgrz.readiness.gaps.list", 1)]
public sealed record ListReadinessGaps(Uuid TenantId, Uuid ProgramId, Uuid AssessmentId,
    string? PlanState = null, int? Limit = null, string? Cursor = null,
    Uuid? OwnerMemberId = null, string? Kind = null, string? RuleId = null,
    string? Subject = null)
    : IRequest<Page<ReadinessGapView>>, IProgramReadRequest, ICallable;
