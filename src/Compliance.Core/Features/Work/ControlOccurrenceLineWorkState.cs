using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed record ControlOccurrenceLineWorkState(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long PlanRevision, Uuid? CurrentPlanVersionId, Uuid? PendingPlanVersionId);
