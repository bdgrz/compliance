using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

static class RiskGovernanceWorkItemDirectorySchema
{
    public static readonly KvDirectory<AccountableWorkItemProjectionRevision, string> Revisions =
        new("projection_revisions",
            ComplianceCoreJsonContext.Default.AccountableWorkItemProjectionRevision,
            static revision => revision.ProjectorName,
            static projectorName => [projectorName], []);

    public static readonly KvDirectory<RiskGovernanceWorkRevision, string> RiskRevisions = new(
        "risk_revisions", ComplianceCoreJsonContext.Default.RiskGovernanceWorkRevision,
        static revision => RiskKey(revision.ProgramId, revision.RiskId),
        static key => [key], []);

    public static readonly KvDirectory<RiskTreatmentActionWorkState, string> TreatmentActions = new(
        "treatment_actions", ComplianceCoreJsonContext.Default.RiskTreatmentActionWorkState,
        static state => ActionKey(state.ProgramId, state.RiskId, state.Action.ActionId),
        static key => [key], []);

    public static readonly KvDirectoryIndex<AccountableWorkItemView> ByProgram = new(
        "by_program", 1, static item =>
            [item.ProgramId.ToString(), item.WorkItemId.ToString()]);

    public static readonly KvDirectory<AccountableWorkItemView, Uuid> WorkItems = new(
        "accountable_work_items", ComplianceCoreJsonContext.Default.AccountableWorkItemView,
        static item => item.WorkItemId,
        static workItemId => [workItemId.ToString()], [ByProgram]);

    public static string RiskKey(Uuid programId, Uuid riskId) =>
        $"{programId}\n{riskId}";

    public static string ActionKey(Uuid programId, Uuid riskId, Uuid actionId) =>
        $"{programId}\n{riskId}\n{actionId}";
}
