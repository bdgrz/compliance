using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

[Discriminator("bdgrz.control.occurrence.reviewed", 1)]
public sealed record ControlOccurrenceReviewed(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid OccurrenceId, long Revision, ControlOccurrenceReviewView Review) : DomainEvent;
