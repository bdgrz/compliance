using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

static class ProgramDirectorySchema
{
    public static readonly KvDirectoryIndex<ProgramView> ByName = new(
        "by_name", 1, static program => [program.Name.ToUpperInvariant()]);

    public static readonly KvDirectory<ProgramView, Uuid> Directory = new(
        "programs", ComplianceCoreJsonContext.Default.ProgramView,
        static program => program.ProgramId,
        static programId => [programId.ToString()], [ByName]);

    public static readonly KvDirectoryIndex<ProgramRevisionView> RevisionsByProgram = new(
        "by_program", 1,
        static revision => [revision.ProgramId.ToString(),
            revision.Revision.ToString("D20", CultureInfo.InvariantCulture)]);

    public static readonly KvDirectory<ProgramRevisionView, string> Revisions = new(
        "program_revisions", ComplianceCoreJsonContext.Default.ProgramRevisionView,
        static revision => $"{revision.ProgramId}:{revision.Revision.ToString("D20", CultureInfo.InvariantCulture)}",
        static key => [key], [RevisionsByProgram]);
}
