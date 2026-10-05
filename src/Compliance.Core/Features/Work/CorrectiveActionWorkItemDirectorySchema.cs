using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

static class CorrectiveActionWorkItemDirectorySchema
{
    public static readonly KvDirectory<AccountableWorkItemProjectionRevision, string> Revisions =
        new("projection_revisions",
            ComplianceCoreJsonContext.Default.AccountableWorkItemProjectionRevision,
            static revision => revision.ProjectorName,
            static projectorName => [projectorName], []);

    public static readonly KvDirectory<CorrectiveActionFindingContext, Uuid> FindingContexts =
        new("finding_contexts", ComplianceCoreJsonContext.Default.CorrectiveActionFindingContext,
            static finding => finding.FindingId,
            static findingId => [findingId.ToString()], []);

    public static readonly KvDirectoryIndex<AccountableWorkItemView> ByProgram = new(
        "by_program", 1, static item =>
            [item.ProgramId.ToString(), item.WorkItemId.ToString()]);

    public static readonly KvDirectoryIndex<AccountableWorkItemView> ByFinding = new(
        "by_finding", 1, static item =>
            [item.FindingId!.Value.ToString(), item.WorkItemId.ToString()]);

    public static readonly KvDirectory<AccountableWorkItemView, Uuid> WorkItems = new(
        "accountable_work_items", ComplianceCoreJsonContext.Default.AccountableWorkItemView,
        static item => item.WorkItemId,
        static workItemId => [workItemId.ToString()], [ByProgram, ByFinding]);
}
