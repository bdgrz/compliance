using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     Joins source-derived work with recorded accountability and answers, for one actor, which
///     items exist for them. Eligibility comes from the source workflow: the holder or backup of
///     the work, directly or through current team membership, as an active client member, and never
///     a member the source's separation of duties excludes.
/// </summary>
public sealed class WorkQueueReader(IAggregateReader reader, OperatingAuthority authority,
    TimeProvider clock)
{
    public const int SystemEscalationDays = 7;
    public const int DefaultHorizonDays = 30;

    internal async ValueTask<WorkQueueSnapshot> ReadAsync(Uuid tenantId, Uuid programId,
        OperationsActor actor, int horizonDays, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var candidates = await WorkSource.LoadAsync(reader, tenantId, programId, today,
            today.AddDays(horizonDays), now, ct).ConfigureAwait(false);
        var ledger = await reader.HydrateAsync(new WorkAssignmentLedger(tenantId, programId), ct)
            .ConfigureAwait(false);
        var manages = await authority.ManagesProgramAsync(tenantId, actor, programId, ct)
            .ConfigureAwait(false);
        var entries = new List<WorkQueueEntry>();
        foreach (var candidate in candidates)
        {
            var state = ledger.Read(candidate.WorkItemId);
            var recorded = state.AssigneeMemberId is { } assignee &&
                           await IsEligibleAsync(tenantId, candidate, assignee, ct)
                               .ConfigureAwait(false);
            var item = Compose(candidate, state, recorded, today);
            var eligible = await IsEligibleAsync(tenantId, candidate, actor.MemberId, ct)
                .ConfigureAwait(false);
            var inTeam = candidate.Responsible.Kind == OperatingAuthority.TeamHolder &&
                         await authority.HoldsAsync(tenantId, candidate.Responsible,
                             actor.MemberId, ct).ConfigureAwait(false);
            var visible = manages || eligible || item.AssigneeMemberId == actor.MemberId;
            if (visible)
                entries.Add(new WorkQueueEntry(candidate, state, item, eligible, inTeam));
        }
        var ordered = entries
            .OrderBy(static entry => entry.Item.DueOn ?? DateOnly.MaxValue)
            .ThenBy(static entry => MaterialityRank(entry.Item.Materiality))
            .ThenBy(static entry => entry.Item.CreatedAt)
            .ThenBy(static entry => entry.Item.WorkItemId.ToString(), StringComparer.Ordinal)
            .ToArray();
        return new WorkQueueSnapshot(today, manages, ordered);
    }

    /// <summary>Whether the source workflow would accept this member performing the work.</summary>
    public async ValueTask<bool> IsEligibleAsync(Uuid tenantId, WorkCandidate candidate,
        Uuid memberId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.Excluded.Contains(memberId))
            return false;
        if (!await authority.HoldsAsync(tenantId, candidate.Responsible, memberId, ct)
                .ConfigureAwait(false) &&
            !await authority.HoldsAsync(tenantId, candidate.Backup, memberId, ct)
                .ConfigureAwait(false))
            return false;
        return await authority.IsActiveAsync(tenantId,
            new OperatingHolder(OperatingAuthority.MemberHolder, memberId), ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     The item as read: a recorded assignee who is still eligible, otherwise the direct member
    ///     holder, otherwise unassigned team work. Seven days overdue escalates by system rule.
    /// </summary>
    public static WorkQueueItemView Compose(WorkCandidate candidate, WorkAssignmentState state,
        bool recordedAssigneeEligible, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(state);
        var assignee = recordedAssigneeEligible
            ? state.AssigneeMemberId
            : candidate.Responsible.Kind == OperatingAuthority.MemberHolder
                ? candidate.Responsible.Id
                : (Uuid?)null;
        var escalatedBy = state.Escalated
            ? "member"
            : candidate.DueOn is { } due && due.AddDays(SystemEscalationDays) <= today
                ? "system"
                : null;
        return new WorkQueueItemView(candidate.WorkItemId, candidate.Kind, candidate.SourceId,
            candidate.ControlId, candidate.FindingId, candidate.Summary, candidate.Reason,
            candidate.DueOn, candidate.DueOn < today, candidate.Materiality,
            candidate.NextAction, candidate.ActionPath, candidate.Responsible, assignee,
            state.Revision, escalatedBy is not null, escalatedBy, candidate.CreatedAt);
    }

    static int MaterialityRank(string? materiality) => materiality switch
    {
        "high" => 0,
        "medium" => 1,
        "low" => 2,
        _ => 3,
    };
}
