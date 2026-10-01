namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     What approving a proposed plan would do: its cadence in user language, the next expected
///     occurrences, separation-of-duties conflicts, and the open work it would reassign.
/// </summary>
public sealed record ControlOperatingPlanPreview(string CadenceDescription,
    IReadOnlyList<string> ExpectedEvidence, IReadOnlyList<ControlOccurrenceView> UpcomingOccurrences,
    IReadOnlyList<string> Conflicts, IReadOnlyList<OccurrenceReassignmentView> Reassignments);
