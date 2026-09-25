using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

static class ControlDraftHistoryDirectoryV1Schema
{
    public static readonly KvDirectoryIndex<ControlDraftRevisionView> ByControlRevision = new(
        "by_control_revision", 1, static revision => [revision.ControlId.ToString(),
            revision.Revision.ToString("D20", CultureInfo.InvariantCulture)]);

    public static readonly KvDirectory<ControlDraftRevisionView, string> Revisions = new(
        "control_draft_history_revisions_v1",
        ComplianceCoreJsonContext.Default.ControlDraftRevisionView,
        static revision => RevisionKey(revision.ControlId, revision.Revision),
        static key => [key], [ByControlRevision]);

    public static string RevisionKey(Uuid controlId, long revision) =>
        $"{controlId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";
}
