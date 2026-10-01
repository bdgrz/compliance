namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>The complete set of manually attested facts for one population draft.</summary>
public sealed record AccessPopulationFacts(IReadOnlyList<AccessPrincipalFact> Principals,
    IReadOnlyList<AccessEntitlementFact> Entitlements,
    IReadOnlyList<AccessGroupMemberFact> GroupMembers,
    IReadOnlyList<AccessAssignmentFact> Assignments);
