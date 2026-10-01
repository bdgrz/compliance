using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     The audience a rule selects from one frozen roster snapshot, plus every person with a
///     current work relationship so removals can be explained as movers or leavers.
/// </summary>
public sealed record RosterAudience(IReadOnlyDictionary<Uuid, RosterMatch> Matching,
    IReadOnlySet<Uuid> Present)
{
    public static RosterAudience Evaluate(WorkforceRosterSnapshotView roster, string kind,
        IReadOnlyList<string> teams)
    {
        ArgumentNullException.ThrowIfNull(roster);
        var names = roster.People.ToDictionary(static person => person.PersonId,
            static person => person.DisplayName);
        var matching = new Dictionary<Uuid, RosterMatch>();
        var present = new HashSet<Uuid>();
        foreach (var relationship in roster.WorkRelationships
                     .OrderBy(static relationship => relationship.SourceWorkerId,
                         StringComparer.Ordinal))
        {
            if (relationship.LifecycleStatus != "ended")
                present.Add(relationship.PersonId);
            if (!PolicyAudience.Matches(kind, teams, relationship))
                continue;
            var name = names.GetValueOrDefault(relationship.PersonId) ?? "Unknown person";
            matching[relationship.PersonId] = matching.TryGetValue(relationship.PersonId,
                out var existing) && existing.StartDate >= relationship.StartDate
                ? existing
                : new RosterMatch(relationship.PersonId, name, relationship.WorkerType,
                    relationship.Department, relationship.StartDate);
        }
        return new RosterAudience(matching, present);
    }
}
