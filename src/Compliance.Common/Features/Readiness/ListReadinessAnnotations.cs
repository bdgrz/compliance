using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Lists advisor feedback recorded on one assessment's gaps, oldest first.</summary>
[Discriminator("bdgrz.readiness.annotations.list", 1)]
public sealed record ListReadinessAnnotations(Uuid TenantId, Uuid ProgramId, Uuid AssessmentId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<ReadinessAnnotationView>>, IProgramReadRequest, ICallable;
