using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

static class EvidenceWorkItemDirectorySchema
{
    public static readonly KvDirectory<EvidenceWorkItemProjectionRevision, string> Revisions = new(
        "projection_revisions", ComplianceCoreJsonContext.Default.EvidenceWorkItemProjectionRevision,
        static revision => revision.ProjectorName,
        static projectorName => [projectorName], []);

    public static readonly KvDirectoryIndex<AccountableWorkItemView> ByProgram = new(
        "by_program", 1, static item =>
            [item.ProgramId.ToString(), item.WorkItemId.ToString()]);

    public static readonly KvDirectory<AccountableWorkItemView, Uuid> WorkItems = new(
        "accountable_work_items", ComplianceCoreJsonContext.Default.AccountableWorkItemView,
        static item => item.WorkItemId,
        static workItemId => [workItemId.ToString()], [ByProgram]);
}

public sealed record EvidenceWorkItemProjectionRevision(string ProjectorName, long Revision);
