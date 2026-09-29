using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

sealed record MemberResponsibilityAssignments(Uuid TenantId, Uuid MemberId,
    IReadOnlyList<ResponsibilityAssignmentView> Assignments);

static class MemberResponsibilityIndexSchema
{
    public static readonly KvDirectory<ResponsibilityAssignmentView, Uuid> Assignments = new(
        "responsibility_assignments", ComplianceCoreJsonContext.Default.ResponsibilityAssignmentView,
        static item => item.AssignmentId, static id => [id.ToString()], []);

    public static readonly KvDirectory<MemberResponsibilityAssignments, Uuid> Members = new(
        "member_responsibilities", ComplianceCoreJsonContext.Default.MemberResponsibilityAssignments,
        static item => item.MemberId, static id => [id.ToString()], []);
}
