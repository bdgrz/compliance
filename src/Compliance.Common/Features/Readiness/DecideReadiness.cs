using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     Management's personal readiness decision on one assessment: proceed or do_not_proceed.
///     HTTP-only; the member who ran the assessment cannot decide it without an approved waiver.
/// </summary>
[Discriminator("bdgrz.readiness.decide", 1)]
public sealed record DecideReadiness(Uuid TenantId, Uuid ProgramId, Uuid AssessmentId,
    long ExpectedRevision, string Outcome, string Rationale,
    Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<ReadinessDecisionView>, IProgramScopedRequest, ICallable;
