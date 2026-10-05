using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

static class CommitmentDecisionWorkItemDirectorySchema
{
    public static readonly KvDirectory<AccountableWorkItemProjectionRevision, string> Revisions =
        new("projection_revisions",
            ComplianceCoreJsonContext.Default.AccountableWorkItemProjectionRevision,
            static revision => revision.ProjectorName,
            static projectorName => [projectorName], []);

    public static readonly KvDirectoryIndex<CommitmentDecisionWorkState> ByProgram =
        new("by_program", 1, static state =>
            [state.ProgramId.ToString(), state.DraftId.ToString()]);

    public static readonly KvDirectory<CommitmentDecisionWorkState, Uuid> Commitments = new(
        "commitment_decisions", ComplianceCoreJsonContext.Default.CommitmentDecisionWorkState,
        static state => state.DraftId,
        static draftId => [draftId.ToString()], [ByProgram]);
}
