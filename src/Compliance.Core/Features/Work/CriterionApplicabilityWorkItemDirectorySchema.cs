using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

static class CriterionApplicabilityWorkItemDirectorySchema
{
    public static readonly KvDirectory<AccountableWorkItemProjectionRevision, string> Revisions =
        new("projection_revisions",
            ComplianceCoreJsonContext.Default.AccountableWorkItemProjectionRevision,
            static revision => revision.ProjectorName,
            static projectorName => [projectorName], []);

    public static readonly KvDirectory<CriterionApplicabilityWorkState, string> Decisions = new(
        "decisions", ComplianceCoreJsonContext.Default.CriterionApplicabilityWorkState,
        static state => DecisionKey(state.ProgramId, state.DecisionId),
        static key => [key], []);

    public static readonly KvDirectoryIndex<AccountableWorkItemView> ByProgram = new(
        "by_program", 1, static item =>
            [item.ProgramId.ToString(), item.WorkItemId.ToString()]);

    public static readonly KvDirectory<AccountableWorkItemView, Uuid> WorkItems = new(
        "accountable_work_items", ComplianceCoreJsonContext.Default.AccountableWorkItemView,
        static item => item.WorkItemId,
        static workItemId => [workItemId.ToString()], [ByProgram]);

    public static string DecisionKey(Uuid programId, Uuid decisionId) =>
        $"{programId}\n{decisionId}";
}
