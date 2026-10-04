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
    public const string ViewersTeamName = "Viewers";
    public const string TenantAdministrationRoleName = "Org Admin";
    public const string ComplianceManagementRoleName = "Compliance Lead";
    public const string ComplianceParticipationRoleName = "Contributor";
    public const string ViewerRoleName = "Viewer";
    public const string TenantAdministrationRole = "tenant_administration";
    public const string ComplianceManagementRole = "compliance_management";
    public const string ComplianceParticipationRole = "compliance_participation";
    public const string OrgAdminRole = "org_admin";
    public const string ComplianceLeadRole = "compliance_lead";
    public const string ContributorRole = "contributor";
    public const string ViewerRole = "viewer";

    public static Uuid AdministratorsTeamId(Uuid tenantId) => Id(tenantId, "team:administrators");
    public static Uuid PowerUsersTeamId(Uuid tenantId) => Id(tenantId, "team:power-users");
    public static Uuid StandardUsersTeamId(Uuid tenantId) => Id(tenantId, "team:standard-users");
    public static Uuid ViewersTeamId(Uuid tenantId) => Id(tenantId, "team:viewers");
    public static Uuid TenantAdministrationRoleId(Uuid tenantId) => Id(tenantId, "role:tenant-administration");
    public static Uuid ComplianceManagementRoleId(Uuid tenantId) => Id(tenantId, "role:compliance-management");
    public static Uuid ComplianceParticipationRoleId(Uuid tenantId) => Id(tenantId, "role:compliance-participation");
    public static Uuid ViewerRoleId(Uuid tenantId) => Id(tenantId, "role:viewer");

    public static bool IsBuiltInTeam(Uuid tenantId, Uuid teamId) =>
        teamId == AdministratorsTeamId(tenantId) || teamId == PowerUsersTeamId(tenantId) ||
        teamId == StandardUsersTeamId(tenantId) || teamId == ViewersTeamId(tenantId);

    public static bool IsBuiltInRole(Uuid tenantId, Uuid roleId) =>
        roleId == TenantAdministrationRoleId(tenantId) || roleId == ComplianceManagementRoleId(tenantId) ||
        roleId == ComplianceParticipationRoleId(tenantId) || roleId == ViewerRoleId(tenantId);

    public static Uuid? TeamIdForRole(Uuid tenantId, string role) => role switch
    {
        TenantAdministrationRole => AdministratorsTeamId(tenantId),
        OrgAdminRole => AdministratorsTeamId(tenantId),
        ComplianceManagementRole => PowerUsersTeamId(tenantId),
        ComplianceLeadRole => PowerUsersTeamId(tenantId),
        ComplianceParticipationRole => StandardUsersTeamId(tenantId),
        ContributorRole => StandardUsersTeamId(tenantId),
        ViewerRole => ViewersTeamId(tenantId),
        _ => null,
    };

    public static Uuid? RoleIdForRole(Uuid tenantId, string role) => role switch
    {
        TenantAdministrationRole => TenantAdministrationRoleId(tenantId),
        OrgAdminRole => TenantAdministrationRoleId(tenantId),
        ComplianceManagementRole => ComplianceManagementRoleId(tenantId),
        ComplianceLeadRole => ComplianceManagementRoleId(tenantId),
        ComplianceParticipationRole => ComplianceParticipationRoleId(tenantId),
        ContributorRole => ComplianceParticipationRoleId(tenantId),
        ViewerRole => ViewerRoleId(tenantId),
        _ => null,
    };

    static Uuid Id(Uuid tenantId, string name) => Uuid.CreateVersion5(NamespaceId, $"{tenantId}\n{name}");
}
