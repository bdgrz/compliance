using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

[Discriminator("bdgrz.readiness.gap.annotated", 1)]
public sealed record ReadinessGapAnnotated(Uuid TenantId, Uuid ProgramId, long Revision,
    ReadinessAnnotationView Annotation) : DomainEvent;
