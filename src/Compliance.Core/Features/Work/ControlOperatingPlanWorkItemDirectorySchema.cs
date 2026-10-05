using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

static class ControlOperatingPlanWorkItemDirectorySchema
{
    public static readonly KvDirectory<AccountableWorkItemProjectionRevision, string> Revisions =
        new("projection_revisions",
            ComplianceCoreJsonContext.Default.AccountableWorkItemProjectionRevision,
            static revision => revision.ProjectorName,
            static projectorName => [projectorName], []);

    public static readonly KvDirectory<ControlOperatingPlanLineWorkState, string> Lines = new(
        "plan_lines", ComplianceCoreJsonContext.Default.ControlOperatingPlanLineWorkState,
        static line => LineKey(line.ProgramId, line.ControlId),
        static key => [key], []);

    public static readonly KvDirectoryIndex<ControlOperatingPlanApprovalWorkState> ByProgram =
        new("by_program", 1, static work =>
            [work.ProgramId.ToString(), work.PlanVersionId.ToString()]);

    public static readonly KvDirectory<ControlOperatingPlanApprovalWorkState, Uuid> Plans = new(
        "pending_approvals",
        ComplianceCoreJsonContext.Default.ControlOperatingPlanApprovalWorkState,
        static work => work.PlanVersionId,
        static planVersionId => [planVersionId.ToString()], [ByProgram]);

    public static string LineKey(Uuid programId, Uuid controlId) =>
        $"{programId}\n{controlId}";
}
