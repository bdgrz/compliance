using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public static class BuiltInRbac
{
    static Uuid NamespaceId { get; } =
        Uuid.Parse("9af1541d-06cc-5448-889f-68503630fc71", CultureInfo.InvariantCulture);

    public const string AdministratorsTeamName = "Administrators";
    public const string PowerUsersTeamName = "Power Users";
    public const string StandardUsersTeamName = "Standard Users";
    public const string TenantAdministrationRoleName = "Tenant Administration";
    public const string ComplianceManagementRoleName = "Compliance Management";
    public const string ComplianceParticipationRoleName = "Compliance Participation";

    public static Uuid AdministratorsTeamId(Uuid tenantId) => Id(tenantId, "team:administrators");
    public static Uuid PowerUsersTeamId(Uuid tenantId) => Id(tenantId, "team:power-users");
    public static Uuid StandardUsersTeamId(Uuid tenantId) => Id(tenantId, "team:standard-users");
    public static Uuid TenantAdministrationRoleId(Uuid tenantId) => Id(tenantId, "role:tenant-administration");
    public static Uuid ComplianceManagementRoleId(Uuid tenantId) => Id(tenantId, "role:compliance-management");
    public static Uuid ComplianceParticipationRoleId(Uuid tenantId) => Id(tenantId, "role:compliance-participation");

    public static bool IsBuiltInTeam(Uuid tenantId, Uuid teamId) =>
        teamId == AdministratorsTeamId(tenantId) || teamId == PowerUsersTeamId(tenantId) ||
        teamId == StandardUsersTeamId(tenantId);

    public static bool IsBuiltInRole(Uuid tenantId, Uuid roleId) =>
        roleId == TenantAdministrationRoleId(tenantId) || roleId == ComplianceManagementRoleId(tenantId) ||
        roleId == ComplianceParticipationRoleId(tenantId);

    static Uuid Id(Uuid tenantId, string name) => Uuid.CreateVersion5(NamespaceId, $"{tenantId}\n{name}");
}
