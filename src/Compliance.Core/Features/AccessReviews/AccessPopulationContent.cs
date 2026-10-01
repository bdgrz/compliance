using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Snapshots;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     The canonical v1 rows of an accepted access population: one header row and one row per
///     observed principal, entitlement, membership, and direct assignment, keyed by provider IDs.
/// </summary>
public static class AccessPopulationContent
{
    public const string Kind = "access_population";
    const string HeaderKey = "header";
    const string PrincipalPrefix = "principal/";
    const string EntitlementPrefix = "entitlement/";
    const string MemberPrefix = "group_member/";
    const string AssignmentPrefix = "access_assignment/";

    public static IReadOnlyList<PopulationRow> Rows(AccessPopulationHeader header,
        AccessPopulationFacts facts)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(facts);
        var json = ComplianceCoreJsonContext.Default;
        var rows = new List<PopulationRow>
        {
            new(HeaderKey, JsonSerializer.SerializeToElement(header, json.AccessPopulationHeader)),
        };
        rows.AddRange(facts.Principals.Select(principal => new PopulationRow(
            PrincipalPrefix + principal.ProviderSubjectId,
            JsonSerializer.SerializeToElement(principal, json.AccessPrincipalFact))));
        rows.AddRange(facts.Entitlements.Select(entitlement => new PopulationRow(
            EntitlementPrefix + entitlement.ProviderEntitlementId,
            JsonSerializer.SerializeToElement(entitlement, json.AccessEntitlementFact))));
        rows.AddRange(facts.GroupMembers.Select(member => new PopulationRow(
            MemberPrefix + Pair(member.GroupProviderSubjectId, member.MemberProviderSubjectId),
            JsonSerializer.SerializeToElement(member, json.AccessGroupMemberFact))));
        rows.AddRange(facts.Assignments.Select(assignment => new PopulationRow(
            AssignmentPrefix + Pair(assignment.PrincipalProviderSubjectId,
                assignment.ProviderEntitlementId),
            JsonSerializer.SerializeToElement(assignment, json.AccessAssignmentFact))));
        SnapshotRows.Sort(rows);
        return rows;
    }

    public static (AccessPopulationHeader Header, AccessPopulationFacts Facts) Read(
        PopulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var json = ComplianceCoreJsonContext.Default;
        AccessPopulationHeader? header = null;
        var principals = new List<AccessPrincipalFact>();
        var entitlements = new List<AccessEntitlementFact>();
        var members = new List<AccessGroupMemberFact>();
        var assignments = new List<AccessAssignmentFact>();
        foreach (var row in snapshot.Rows)
        {
            if (row.Key == HeaderKey)
                header = row.Content.Deserialize(json.AccessPopulationHeader);
            else if (row.Key.StartsWith(PrincipalPrefix, StringComparison.Ordinal))
                principals.Add(row.Content.Deserialize(json.AccessPrincipalFact)!);
            else if (row.Key.StartsWith(EntitlementPrefix, StringComparison.Ordinal))
                entitlements.Add(row.Content.Deserialize(json.AccessEntitlementFact)!);
            else if (row.Key.StartsWith(MemberPrefix, StringComparison.Ordinal))
                members.Add(row.Content.Deserialize(json.AccessGroupMemberFact)!);
            else if (row.Key.StartsWith(AssignmentPrefix, StringComparison.Ordinal))
                assignments.Add(row.Content.Deserialize(json.AccessAssignmentFact)!);
            else
                throw new SnapshotContentException("The population snapshot holds an unknown row kind.");
        }
        return (header ?? throw new SnapshotContentException(
            "The population snapshot has no header."), new AccessPopulationFacts(principals,
            entitlements, members, assignments));
    }

    /// <summary>A collision-free key for an ordered pair of provider IDs.</summary>
    static string Pair(string left, string right) => Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes(left + "\0" + right)));
}
