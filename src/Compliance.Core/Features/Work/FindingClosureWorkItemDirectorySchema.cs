using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

static class FindingClosureWorkItemDirectorySchema
{
    public static readonly KvDirectoryIndex<FindingClosureWorkState> ByProgram =
        new("by_program", 1, static finding =>
            [finding.ProgramId.ToString(), finding.FindingId.ToString()]);

    public static readonly KvDirectory<FindingClosureWorkState, Uuid> Findings = new(
        "findings", ComplianceCoreJsonContext.Default.FindingClosureWorkState,
        static finding => finding.FindingId,
        static findingId => [findingId.ToString()], [ByProgram]);
}
