using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

static class CommitmentDraftHistoryDirectoryV1Schema
{
    public static readonly KvDirectoryIndex<CommitmentDraftRevisionView> ByDraftRevision = new(
        "by_draft_revision", 1, static revision =>
            [revision.DraftId.ToString(),
                revision.Revision.ToString("D20", CultureInfo.InvariantCulture)]);

    public static readonly KvDirectory<CommitmentDraftRevisionView, string> Revisions = new(
        "commitment_draft_revisions_v1",
        ComplianceCoreJsonContext.Default.CommitmentDraftRevisionView,
        static revision => RevisionKey(revision.DraftId, revision.Revision), static key => [key],
        [ByDraftRevision]);

    public static string RevisionKey(Uuid draftId, long revision) =>
        $"{draftId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";
}
