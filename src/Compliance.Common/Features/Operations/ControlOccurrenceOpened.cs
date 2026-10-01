using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

[Discriminator("bdgrz.control.occurrence.opened", 1)]
public sealed record ControlOccurrenceOpened(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid OccurrenceId, long Revision, string Kind, Uuid ControlVersionId, Uuid PlanVersionId,
    DateOnly? PeriodStart, DateOnly? PeriodEnd, DateOnly? DueOn, string? Trigger,
    OperatingHolder Assignee, ActorReference OpenedBy, DateTimeOffset OpenedAt) : DomainEvent;
