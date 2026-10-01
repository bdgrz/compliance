using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

static class PolicyDirectorySchema
{
    public static readonly KvDirectoryIndex<PolicyDirectoryRow> ByProgramIdentifier = new(
        "by_program_identifier", 1,
        static row => [row.Summary.ProgramId.ToString(), row.Summary.Identifier]);

    public static readonly KvDirectory<PolicyDirectoryRow, Uuid> Policies = new(
        "policies", ComplianceCoreJsonContext.Default.PolicyDirectoryRow,
        static row => row.Summary.PolicyId, static policyId => [policyId.ToString()],
        [ByProgramIdentifier]);
}
