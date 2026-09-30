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

    public static readonly KvDirectoryIndex<CommitmentVersionView> ByDraftVersion = new(
        "by_draft_version", 1, static version =>
            [version.DraftId.ToString(), version.Version.ToString("D20", CultureInfo.InvariantCulture)]);

    public static readonly KvDirectory<CommitmentVersionView, string> Versions = new(
        "commitment_versions", ComplianceCoreJsonContext.Default.CommitmentVersionView,
        static version => RevisionKey(version.DraftId, version.Version), static key => [key],
        [ByDraftVersion]);

    public static readonly KvDirectoryIndex<CommitmentDecisionView> ByDraftDecision = new(
        "by_draft_decided_at", 1, static decision =>
            [decision.DraftId.ToString(),
                decision.DecidedAt.UtcTicks.ToString("D20", CultureInfo.InvariantCulture),
                decision.DecisionId.ToString()]);

    public static readonly KvDirectory<CommitmentDecisionView, Uuid> Decisions = new(
        "commitment_decisions", ComplianceCoreJsonContext.Default.CommitmentDecisionView,
        static decision => decision.DecisionId, static decisionId => [decisionId.ToString()],
        [ByDraftDecision]);

    public static string RevisionKey(Uuid draftId, long revision) =>
        $"{draftId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";
}
