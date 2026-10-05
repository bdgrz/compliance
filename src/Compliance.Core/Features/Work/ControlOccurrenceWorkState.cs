using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed record ControlOccurrenceWorkState(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid OccurrenceId, long Revision, string Kind, Uuid ControlVersionId, Uuid PlanVersionId,
    DateOnly? PeriodStart, DateOnly? PeriodEnd, DateOnly? DueOn, OperatingHolder Assignee,
    string State, ControlOccurrenceAttestationWorkState? LatestAttestation);
