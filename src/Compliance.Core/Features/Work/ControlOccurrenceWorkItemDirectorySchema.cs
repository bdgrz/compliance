using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

static class ControlOccurrenceWorkItemDirectorySchema
{
    public static readonly KvDirectory<AccountableWorkItemProjectionRevision, string> Revisions =
        new("projection_revisions",
            ComplianceCoreJsonContext.Default.AccountableWorkItemProjectionRevision,
            static revision => revision.ProjectorName,
            static projectorName => [projectorName], []);

    public static readonly KvDirectory<ControlOccurrenceLineWorkState, string> Lines = new(
        "plan_lines", ComplianceCoreJsonContext.Default.ControlOccurrenceLineWorkState,
        static line => LineKey(line.ProgramId, line.ControlId),
        static key => [key], []);

    public static readonly KvDirectoryIndex<ControlOccurrencePlanWorkState> PlansByProgram = new(
        "plans_by_program", 1, static plan =>
            [plan.ProgramId.ToString(), plan.ControlId.ToString(), plan.PlanVersionId.ToString()]);

    public static readonly KvDirectory<ControlOccurrencePlanWorkState, string> Plans = new(
        "plans", ComplianceCoreJsonContext.Default.ControlOccurrencePlanWorkState,
        static plan => PlanKey(plan.ProgramId, plan.ControlId, plan.PlanVersionId),
        static key => [key], [PlansByProgram]);

    public static readonly KvDirectoryIndex<ControlOccurrenceWorkState> OccurrencesByProgram =
        new("occurrences_by_program", 1, static occurrence =>
            [occurrence.ProgramId.ToString(), occurrence.OccurrenceId.ToString()]);

    public static readonly KvDirectory<ControlOccurrenceWorkState, string> Occurrences = new(
        "occurrences", ComplianceCoreJsonContext.Default.ControlOccurrenceWorkState,
        static occurrence => OccurrenceKey(occurrence.ProgramId, occurrence.OccurrenceId),
        static key => [key], [OccurrencesByProgram]);

    public static string LineKey(Uuid programId, Uuid controlId) =>
        $"{programId}\n{controlId}";

    public static string PlanKey(Uuid programId, Uuid controlId, Uuid planVersionId) =>
        $"{programId}\n{controlId}\n{planVersionId}";

    public static string OccurrenceKey(Uuid programId, Uuid occurrenceId) =>
        $"{programId}\n{occurrenceId}";
}
