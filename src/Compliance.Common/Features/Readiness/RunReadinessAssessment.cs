using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     Runs the manual readiness rules for a program as of a time (now by default). The run
///     records its exact inputs and results; it is HTTP-only and never an audit opinion.
/// </summary>
[Discriminator("bdgrz.readiness.assessment.run", 1)]
public sealed record RunReadinessAssessment(Uuid TenantId, Uuid ProgramId,
    long ExpectedRevision, DateTimeOffset? AsOf = null)
    : IRequest<ReadinessAssessmentRegistration>, IProgramScopedRequest, ICallable;
