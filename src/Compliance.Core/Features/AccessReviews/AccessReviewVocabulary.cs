namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>The M0-D07 access-review vocabulary.</summary>
public static class AccessReviewVocabulary
{
    public const string Group = "group";
    public const string Role = "role";
    public const string UserAccount = "user_account";
    public const string ManualAttestation = "manual_attestation";

    public const string Human = "human";
    public const string Nhi = "nhi";
    public const string Shared = "shared";
    public const string Unclassified = "unclassified";

    public const string Keep = "keep";
    public const string Modify = "modify";
    public const string Revoke = "revoke";
    public const string UnableToDetermine = "unable_to_determine";

    public const string ForbiddenPrincipalKind = "forbidden_principal_kind";
    public const string PrivilegedEntitlement = "privileged_entitlement";
    public const string RequiresExpiry = "requires_expiry";
    public const string RequiredAccess = "required_access";

    public static readonly IReadOnlySet<string> PrincipalKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        UserAccount, Group, Role, "service_principal", "workload_identity", "access_token",
        "app_installation",
    };

    /// <summary>Principal kinds that a provider hint proposes as non-human.</summary>
    public static readonly IReadOnlySet<string> NonHumanKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        "service_principal", "workload_identity", "access_token", "app_installation",
    };

    public static readonly IReadOnlySet<string> EntitlementKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        "membership", "role_assignment", "permission_set_assignment", "permission",
        "repository_access", "admin_privilege",
    };

    public static readonly IReadOnlySet<string> Classifications = new HashSet<string>(StringComparer.Ordinal)
    {
        Human, Nhi, Shared, Unclassified,
    };

    public static readonly IReadOnlySet<string> Decisions = new HashSet<string>(StringComparer.Ordinal)
    {
        Keep, Modify, Revoke, UnableToDetermine,
    };

    public static readonly IReadOnlySet<string> RuleKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        ForbiddenPrincipalKind, PrivilegedEntitlement, RequiresExpiry, RequiredAccess,
    };

    /// <summary>Groups and roles convey access; they are never review subjects.</summary>
    public static bool IsAccessStructure(string principalKind) =>
        principalKind is Group or Role;

    public static bool RequiresRemediation(string? decision) => decision is Modify or Revoke;

    public static bool IsBoundedText(string? value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximum;
}
