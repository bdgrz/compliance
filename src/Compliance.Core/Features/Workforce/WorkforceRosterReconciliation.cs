using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     Reconciles the governed manual roster with tenant platform memberships (M0-D06). Findings are
///     shown for an attributable decision and never silently resolved, and nothing here grants or
///     revokes access. Firm staff access comes from engagements, not the roster, so firm staff and
///     suspended members are never access-only. The manager chain is not consulted.
/// </summary>
public static class WorkforceRosterReconciliation
{
    public static IReadOnlyList<WorkforceReconciliationObservationView> Evaluate(Uuid tenantId,
        IReadOnlyCollection<PersonView> people, IReadOnlyCollection<WorkRelationshipView> relationships,
        IReadOnlyCollection<TenantMembershipView> memberships, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(people);
        ArgumentNullException.ThrowIfNull(relationships);
        ArgumentNullException.ThrowIfNull(memberships);
        var roster = people.Where(person => person.TenantId == tenantId).ToList();
        var jobs = relationships.Where(job => job.TenantId == tenantId)
            .ToLookup(static job => job.PersonId);
        var members = memberships.Where(member => member.TenantId == tenantId)
            .GroupBy(static member => member.UserId)
            .ToDictionary(static group => group.Key, static group => group.First());
        var found = new List<WorkforceReconciliationObservationView>();
        void Add(string kind, string reason, string key, IEnumerable<Uuid> personIds,
            IEnumerable<Uuid> relationshipIds, IEnumerable<Uuid> userIds) =>
            found.Add(new WorkforceReconciliationObservationView(tenantId,
                Uuid.CreateVersion5(Uuid.CreateVersion5(tenantId, "workforce-reconciliation"),
                    $"{kind}:{reason}:{key}"),
                kind, reason, "open", Sorted(personIds), Sorted(relationshipIds), Sorted(userIds)));

        foreach (var person in roster)
        {
            var personJobs = jobs[person.PersonId].ToList();
            if (personJobs.Count == 0)
                Add("missing", "no_work_relationship", person.PersonId.ToString(),
                    [person.PersonId], [], Optional(person.CorrelatedUserId));
            if (person.CorrelatedUserId is not { } userId)
                continue;
            if (!members.TryGetValue(userId, out var member))
                Add("stale", "correlated_member_missing", $"{person.PersonId}:{userId}",
                    [person.PersonId], [], [userId]);
            else if (!member.IsSuspended && personJobs.Count > 0 &&
                     personJobs.All(static job => job.LifecycleStatus == "ended"))
                Add("conflicting", "ended_worker_retains_access",
                    $"{person.PersonId}:{userId}:{Versions(personJobs)}", [person.PersonId],
                    personJobs.Select(static job => job.RelationshipId), [userId]);
        }

        foreach (var shared in roster.Where(static person => person.CorrelatedUserId is not null)
                     .GroupBy(static person => person.CorrelatedUserId!.Value)
                     .Where(static group => group.Count() > 1))
        {
            var ids = Sorted(shared.Select(static person => person.PersonId));
            Add("duplicate", "member_correlated_to_several_people",
                $"{shared.Key}:{string.Join(',', ids)}", ids, [], [shared.Key]);
        }

        foreach (var shared in roster.Where(static person => person.WorkEmail is not null)
                     .GroupBy(static person => person.WorkEmail!.ToUpperInvariant())
                     .Where(static group => group.Count() > 1))
        {
            var ids = Sorted(shared.Select(static person => person.PersonId));
            Add("duplicate", "shared_work_email", string.Join(',', ids), ids, [], []);
        }

        foreach (var job in jobs.SelectMany(static group => group))
        {
            if (job.LifecycleStatus != "ended" && job.EndDate is { } end && end < today)
                Add("stale", "end_date_passed", $"{job.RelationshipId}:{job.Revision}",
                    [job.PersonId], [job.RelationshipId], []);
            if (job.LifecycleStatus == "pending" && job.StartDate < today)
                Add("stale", "start_date_passed", $"{job.RelationshipId}:{job.Revision}",
                    [job.PersonId], [job.RelationshipId], []);
        }

        var correlated = roster.Select(static person => person.CorrelatedUserId)
            .OfType<Uuid>().ToHashSet();
        foreach (var member in members.Values.Where(member => !member.IsSuspended &&
                     member.Affiliation != "firm_staff" && !correlated.Contains(member.UserId)))
            Add("access_only", "member_not_on_roster", member.UserId.ToString(), [], [],
                [member.UserId]);

        return [.. found.OrderBy(static item => item.ObservationId.ToString(),
            StringComparer.Ordinal)];
    }

    static Uuid[] Sorted(IEnumerable<Uuid> ids) =>
        [.. ids.Distinct().OrderBy(static id => id.ToString(), StringComparer.Ordinal)];

    static IEnumerable<Uuid> Optional(Uuid? id) => id is { } value ? [value] : [];

    static string Versions(IEnumerable<WorkRelationshipView> jobs) =>
        string.Join(',', jobs.OrderBy(static job => job.RelationshipId.ToString(),
                StringComparer.Ordinal)
            .Select(static job => $"{job.RelationshipId}@{job.Revision}"));
}
