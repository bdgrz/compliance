using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

static class BoundaryDecisionWorkItemDirectorySchema
{
    public static readonly KvDirectory<AccountableWorkItemProjectionRevision, string> Revisions =
        new("projection_revisions",
            ComplianceCoreJsonContext.Default.AccountableWorkItemProjectionRevision,
            static revision => revision.ProjectorName,
            static projectorName => [projectorName], []);

    public static readonly KvDirectoryIndex<BoundaryDecisionWorkState> ByProgram =
        new("by_program", 1, static state =>
            [state.ProgramId.ToString(), state.BoundaryId.ToString()]);

    public static readonly KvDirectory<BoundaryDecisionWorkState, Uuid> Boundaries = new(
        "boundary_decisions", ComplianceCoreJsonContext.Default.BoundaryDecisionWorkState,
        static state => state.BoundaryId,
        static boundaryId => [boundaryId.ToString()], [ByProgram]);
}
