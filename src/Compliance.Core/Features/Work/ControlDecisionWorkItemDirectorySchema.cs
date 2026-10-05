using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

static class ControlDecisionWorkItemDirectorySchema
{
    public static readonly KvDirectory<AccountableWorkItemProjectionRevision, string> Revisions =
        new("projection_revisions",
            ComplianceCoreJsonContext.Default.AccountableWorkItemProjectionRevision,
            static revision => revision.ProjectorName,
            static projectorName => [projectorName], []);

    public static readonly KvDirectoryIndex<ControlDecisionWorkState> ByProgram = new(
        "by_program", 1, static state =>
            [state.ProgramId.ToString(), state.ControlId.ToString()]);

    public static readonly KvDirectory<ControlDecisionWorkState, Uuid> Controls = new(
        "control_decisions", ComplianceCoreJsonContext.Default.ControlDecisionWorkState,
        static state => state.ControlId,
        static controlId => [controlId.ToString()], [ByProgram]);
}
