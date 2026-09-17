using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

static class RbacIds
{
    static Uuid MemberNamespaceId { get; } =
        Uuid.Parse("865f03c8-fe86-5da6-804d-e99e4cecb501", CultureInfo.InvariantCulture);
    static Uuid TeamMemberNamespaceId { get; } =
        Uuid.Parse("bd613239-bae3-59d4-a075-1cbb40c58f4d", CultureInfo.InvariantCulture);
    static Uuid TeamRoleNamespaceId { get; } =
        Uuid.Parse("70ab7499-50b7-574d-9d86-e2db38a61855", CultureInfo.InvariantCulture);
    static Uuid RolePermissionNamespaceId { get; } =
        Uuid.Parse("ad272ced-02a7-5716-8600-1bd26ea70d6b", CultureInfo.InvariantCulture);

    public static Uuid Member(Uuid tenantId, Uuid userId) =>
        Uuid.CreateVersion5(MemberNamespaceId, $"{tenantId}\n{userId}");

    public static Uuid TeamMember(Uuid tenantId, Uuid teamId, Uuid memberId) =>
        Uuid.CreateVersion5(TeamMemberNamespaceId, $"{tenantId}\n{teamId}\n{memberId}");

    public static Uuid TeamRole(Uuid tenantId, Uuid teamId, Uuid roleId) =>
        Uuid.CreateVersion5(TeamRoleNamespaceId, $"{tenantId}\n{teamId}\n{roleId}");

    public static Uuid RolePermission(Uuid tenantId, Uuid roleId, string permission) =>
        Uuid.CreateVersion5(RolePermissionNamespaceId, $"{tenantId}\n{roleId}\n{permission}");
}
