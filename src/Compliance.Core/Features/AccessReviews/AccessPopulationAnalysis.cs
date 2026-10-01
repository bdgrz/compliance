using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Validates attested facts and derives effective access (M0-D07). Nested groups are expanded
///     transitively, role assumption is recorded as its own hop, cycles are rejected, and each
///     subject-entitlement pair appears once with every path that conveys it.
/// </summary>
public static class AccessPopulationAnalysis
{
    /// <summary>Rows besides the header that one inline population snapshot can hold.</summary>
    public const int MaximumFacts = 480;

    public const string CalculationVersion = "effective-access-v1";
    const int MaximumPathsPerPair = 16;
    const int MaximumDepth = 32;

    static readonly Uuid CalculationNamespace =
        Uuid.CreateVersion5(Uuid.Empty, "bdgrz.access_review.effective_access");

    public static int FactCount(AccessPopulationFacts facts) => facts.Principals.Count +
        facts.Entitlements.Count + facts.GroupMembers.Count + facts.Assignments.Count;

    /// <summary>The calculation identity is the algorithm version over the frozen content identity.</summary>
    public static Uuid CalculationId(string contentSha256) =>
        Uuid.CreateVersion5(CalculationNamespace, CalculationVersion + ":" + contentSha256);

    public static IReadOnlyList<AccessPopulationIssue> Validate(AccessPopulationFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var issues = new List<AccessPopulationIssue>();
        if (FactCount(facts) > MaximumFacts)
            issues.Add(new("invalid", "too_many_facts", "population",
                $"A population currently supports at most {MaximumFacts} facts."));
        if (facts.Principals.Count == 0)
            issues.Add(new("incomplete", "no_principals", "population",
                "No principals were attested; missing source data is never treated as zero access."));

        var principals = new Dictionary<string, AccessPrincipalFact>(StringComparer.Ordinal);
        var folded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var principal in facts.Principals)
        {
            var id = principal.ProviderSubjectId;
            if (!IsIdentifier(id))
            {
                issues.Add(new("invalid", "invalid_principal_id", id ?? string.Empty,
                    "A principal requires a trimmed provider subject ID of at most 512 characters."));
                continue;
            }
            if (!AccessReviewVocabulary.PrincipalKinds.Contains(principal.PrincipalKind ?? string.Empty))
                issues.Add(new("invalid", "unknown_principal_kind", id,
                    $"The principal kind '{principal.PrincipalKind}' is not recognized."));
            if (!AccessReviewVocabulary.IsBoundedText(principal.DisplayName, 500) ||
                !AccessReviewVocabulary.IsBoundedText(principal.Status, 100) ||
                principal.Email is { Length: > 320 })
                issues.Add(new("invalid", "invalid_principal_fields", id,
                    "A principal requires a display name and status, and a bounded email."));
            if (!principals.TryAdd(id, principal))
                issues.Add(new("duplicate", "duplicate_principal", id,
                    "The provider subject ID was attested more than once."));
            else if (folded.TryGetValue(id, out var other))
                issues.Add(new("ambiguous", "ambiguous_principal_id", id,
                    $"The provider subject ID differs from '{other}' only by case."));
            else
                folded[id] = id;
        }

        var entitlements = new HashSet<string>(StringComparer.Ordinal);
        var foldedEntitlements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entitlement in facts.Entitlements)
        {
            var id = entitlement.ProviderEntitlementId;
            if (!IsIdentifier(id))
            {
                issues.Add(new("invalid", "invalid_entitlement_id", id ?? string.Empty,
                    "An entitlement requires a trimmed provider entitlement ID of at most 512 characters."));
                continue;
            }
            if (!AccessReviewVocabulary.EntitlementKinds.Contains(entitlement.EntitlementKind ?? string.Empty))
                issues.Add(new("invalid", "unknown_entitlement_kind", id,
                    $"The entitlement kind '{entitlement.EntitlementKind}' is not recognized."));
            if (!AccessReviewVocabulary.IsBoundedText(entitlement.DisplayName, 500))
                issues.Add(new("invalid", "invalid_entitlement_fields", id,
                    "An entitlement requires a display name of at most 500 characters."));
            if (!entitlements.Add(id))
                issues.Add(new("duplicate", "duplicate_entitlement", id,
                    "The provider entitlement ID was attested more than once."));
            else if (foldedEntitlements.TryGetValue(id, out var other))
                issues.Add(new("ambiguous", "ambiguous_entitlement_id", id,
                    $"The provider entitlement ID differs from '{other}' only by case."));
            else
                foldedEntitlements[id] = id;
        }

        var memberships = new HashSet<(string, string)>();
        foreach (var member in facts.GroupMembers)
        {
            var subject = $"{member.GroupProviderSubjectId} <- {member.MemberProviderSubjectId}";
            if (!principals.TryGetValue(member.GroupProviderSubjectId, out var container) ||
                !principals.ContainsKey(member.MemberProviderSubjectId))
            {
                issues.Add(new("incomplete", "unknown_membership_principal", subject,
                    "A membership names a principal that was not attested."));
                continue;
            }
            if (!AccessReviewVocabulary.IsAccessStructure(container.PrincipalKind))
                issues.Add(new("invalid", "container_not_group_or_role", subject,
                    "Only a group or role principal can have members."));
            if (member.GroupProviderSubjectId == member.MemberProviderSubjectId)
                issues.Add(new("invalid", "self_membership", subject,
                    "A principal cannot be a member of itself."));
            if (!memberships.Add((member.GroupProviderSubjectId, member.MemberProviderSubjectId)))
                issues.Add(new("duplicate", "duplicate_membership", subject,
                    "The membership was attested more than once."));
        }

        var assignments = new HashSet<(string, string)>();
        foreach (var assignment in facts.Assignments)
        {
            var subject = $"{assignment.PrincipalProviderSubjectId} -> {assignment.ProviderEntitlementId}";
            if (!principals.ContainsKey(assignment.PrincipalProviderSubjectId) ||
                !entitlements.Contains(assignment.ProviderEntitlementId))
            {
                issues.Add(new("incomplete", "unknown_assignment_reference", subject,
                    "An assignment names a principal or entitlement that was not attested."));
                continue;
            }
            if (!assignments.Add((assignment.PrincipalProviderSubjectId, assignment.ProviderEntitlementId)))
                issues.Add(new("duplicate", "duplicate_assignment", subject,
                    "The direct assignment was attested more than once."));
        }

        if (FindCycle(facts) is { } cycle)
            issues.Add(new("invalid", "membership_cycle", cycle,
                "Nested memberships form a cycle; effective access cannot be derived."));
        return issues;
    }

    /// <summary>Derives effective access from facts that passed validation.</summary>
    public static IReadOnlyList<EffectiveAccessView> Derive(AccessPopulationFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var kinds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var principal in facts.Principals)
            kinds.TryAdd(principal.ProviderSubjectId, principal.PrincipalKind);
        var containers = Containers(facts);
        var granted = facts.Assignments
            .GroupBy(static assignment => assignment.PrincipalProviderSubjectId, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group
                .OrderBy(static assignment => assignment.ProviderEntitlementId, StringComparer.Ordinal)
                .ToArray(), StringComparer.Ordinal);

        var rows = new List<EffectiveAccessView>();
        foreach (var subject in kinds.Where(static pair => !AccessReviewVocabulary.IsAccessStructure(pair.Value))
                     .Select(static pair => pair.Key).Order(StringComparer.Ordinal))
        {
            var paths = new SortedDictionary<string, List<EffectiveAccessPathView>>(StringComparer.Ordinal);
            Walk(subject, [], new HashSet<string>(StringComparer.Ordinal) { subject });
            foreach (var (entitlement, found) in paths)
            {
                var direct = found.Any(static path => path.Hops.Count == 1);
                var expiresAt = found.Any(static path => path.ExpiresAt is null)
                    ? null
                    : found.Max(static path => path.ExpiresAt);
                rows.Add(new EffectiveAccessView(subject, entitlement, direct, found, expiresAt));
            }

            void Walk(string node, List<EffectiveAccessHop> hops, HashSet<string> visited)
            {
                if (hops.Count > MaximumDepth)
                    return;
                foreach (var assignment in granted.GetValueOrDefault(node) ?? [])
                {
                    if (!paths.TryGetValue(assignment.ProviderEntitlementId, out var list))
                        paths[assignment.ProviderEntitlementId] = list = [];
                    if (list.Count < MaximumPathsPerPair)
                        list.Add(new EffectiveAccessPathView([.. hops, new EffectiveAccessHop(
                            "access_assignment", node, assignment.ProviderEntitlementId)],
                            assignment.ExpiresAt));
                }
                foreach (var container in containers.GetValueOrDefault(node) ?? [])
                {
                    if (!visited.Add(container))
                        continue;
                    var kind = kinds.GetValueOrDefault(container) == AccessReviewVocabulary.Role
                        ? "role_assumption"
                        : "group_member";
                    Walk(container, [.. hops, new EffectiveAccessHop(kind, node, container)], visited);
                    visited.Remove(container);
                }
            }
        }
        return rows;
    }

    static Dictionary<string, string[]> Containers(AccessPopulationFacts facts) =>
        facts.GroupMembers
            .GroupBy(static member => member.MemberProviderSubjectId, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group
                .Select(static member => member.GroupProviderSubjectId).Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);

    static string? FindCycle(AccessPopulationFacts facts)
    {
        var containers = Containers(facts);
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var start in containers.Keys.Order(StringComparer.Ordinal))
        {
            if (Visit(start) is { } found)
                return found;
        }
        return null;

        string? Visit(string node)
        {
            if (state.TryGetValue(node, out var mark))
                return mark == 1 ? node : null;
            state[node] = 1;
            foreach (var next in containers.GetValueOrDefault(node) ?? [])
            {
                if (Visit(next) is { } found)
                    return found;
            }
            state[node] = 2;
            return null;
        }
    }

    static bool IsIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Length > 512)
            return false;
        try
        {
            return value.IsNormalized(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
