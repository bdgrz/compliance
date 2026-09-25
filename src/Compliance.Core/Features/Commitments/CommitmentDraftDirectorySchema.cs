using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

static class CommitmentDraftDirectorySchema
{
    public static readonly KvDirectoryIndex<CommitmentDraftView> ByProgramKindIdentifier = new(
        "by_program_kind_identifier", 1,
        static draft => [draft.ProgramId.ToString(), draft.Kind, draft.Identifier]);

    public static readonly KvDirectory<CommitmentDraftView, Uuid> Drafts = new(
        "commitment_drafts", ComplianceCoreJsonContext.Default.CommitmentDraftView,
        static draft => draft.DraftId,
        static draftId => [draftId.ToString()], [ByProgramKindIdentifier]);

    public static readonly KvDirectory<CommitmentDraftRevisionView, string> Revisions = new(
        "commitment_draft_revisions", ComplianceCoreJsonContext.Default.CommitmentDraftRevisionView,
        static revision => RevisionKey(revision.DraftId, revision.Revision),
        static key => [key], []);

    public static string RevisionKey(Uuid draftId, long revision) =>
        $"{draftId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";
}
