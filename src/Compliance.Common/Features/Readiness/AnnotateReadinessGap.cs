using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Records attributed advisor feedback on a gap of one exact assessment; HTTP-only.</summary>
[Discriminator("bdgrz.readiness.gap.annotate", 1)]
public sealed record AnnotateReadinessGap(Uuid TenantId, Uuid ProgramId, Uuid AssessmentId,
    Uuid GapId, long ExpectedRevision, string Body)
    : IRequest<ReadinessAnnotationView>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
