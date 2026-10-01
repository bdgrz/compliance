using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     One control occurrence: an expected period from the approved cadence or an event-driven or
///     ad hoc performance. State is expected, missed, open, submitted, returned, approved,
///     action_requested, or deferred.
/// </summary>
public sealed record ControlOccurrenceView(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid OccurrenceId, long Revision, string Kind, string State, Uuid ControlVersionId,
    Uuid PlanVersionId, DateOnly? PeriodStart, DateOnly? PeriodEnd, DateOnly? DueOn,
    string? Trigger, OperatingHolder Assignee, IReadOnlyList<ControlAttestationView> Attestations,
    IReadOnlyList<ControlOccurrenceReviewView> Reviews,
    IReadOnlyList<OccurrenceReassignmentView> Reassignments);
