using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

static class ControlDraftDirectoryV2Schema
{
    public static readonly KvDirectoryIndex<ControlDraftView> ByProgramIdentifier = new(
        "by_program_identifier", 1,
        static control => [control.ProgramId.ToString(), control.Identifier]);

    public static readonly KvDirectory<ControlDraftView, Uuid> Controls = new(
        "control_drafts", ComplianceCoreJsonContext.Default.ControlDraftView,
        static control => control.ControlId,
        static controlId => [controlId.ToString()], [ByProgramIdentifier]);

    public static readonly KvDirectory<ControlDraftRevisionView, string> Revisions = new(
        "control_draft_revisions", ComplianceCoreJsonContext.Default.ControlDraftRevisionView,
        static revision => RevisionKey(revision.ControlId, revision.Revision),
        static key => [key], []);

    public static string RevisionKey(Uuid controlId, long revision) =>
        $"{controlId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";
}
