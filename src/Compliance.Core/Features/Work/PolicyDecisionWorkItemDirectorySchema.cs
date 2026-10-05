using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

static class PolicyDecisionWorkItemDirectorySchema
{
    public static readonly KvDirectory<AccountableWorkItemProjectionRevision, string> Revisions =
        new("projection_revisions",
            ComplianceCoreJsonContext.Default.AccountableWorkItemProjectionRevision,
            static revision => revision.ProjectorName,
            static projectorName => [projectorName], []);

    public static readonly KvDirectoryIndex<PolicyDecisionWorkState> ByProgram =
        new("by_program", 1, static state =>
            [state.ProgramId.ToString(), state.PolicyId.ToString()]);

    public static readonly KvDirectory<PolicyDecisionWorkState, Uuid> Policies = new(
        "policy_decisions", ComplianceCoreJsonContext.Default.PolicyDecisionWorkState,
        static state => state.PolicyId,
        static policyId => [policyId.ToString()], [ByProgram]);
}
