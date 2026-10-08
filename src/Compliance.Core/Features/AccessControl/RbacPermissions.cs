namespace Bdgrz.Compliance.Features.AccessControl;

public static class RbacPermissions
{
    public const string EvidenceArtifactRead = "evidence_artifact.read";
    public const string TenantAccess = "tenant.access";
    public const string TenantRbacManage = "tenant.rbac.manage";
    public const string ProgramManage = "program.manage";
    public const string ApplicationInventoryManage = "application_inventory.manage";
    public const string ApplicationImportStage = "application_import.stage";
    public const string TechnologyInventoryManage = "technology_inventory.manage";
    public const string TechnologyInventoryRestrictedRead = "technology_inventory.restricted.read";
    public const string ApplicationRestrictedRead = "application.restricted.read";
    public const string ProviderInventoryManage = "provider_inventory.manage";
    public const string WorkforceManage = "workforce.manage";
    public const string RiskAcceptComplianceLead = "risk.accept.compliance_lead";
    public const string RiskAcceptExecutive = "risk.accept.executive";
    public const string AccessReviewManage = "access_review.manage";

    public static string? RiskAcceptanceFor(string? authority) => authority switch
    {
        "compliance_lead" => RiskAcceptComplianceLead,
        "executive" => RiskAcceptExecutive,
        _ => null,
    };
}
