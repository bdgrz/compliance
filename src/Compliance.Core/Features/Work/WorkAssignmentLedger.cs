using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     One program's work accountability in one stream: who each source-derived work item is
///     assigned to and whether it was escalated. It never records completion; the source workflow
///     owns that. Optimistic concurrency is per work item.
/// </summary>
public sealed class WorkAssignmentLedger : Aggregate
{
    public const string Claim = "claim";
    public const string Assign = "assign";
    public const string Reassign = "reassign";
    public const string Delegate = "delegate";
    public const string Escalate = "escalate";
    const int MaximumReasonLength = 2000;

    readonly Uuid _tenantId;
    readonly Dictionary<Uuid, Entry> _items = [];

    public WorkAssignmentLedger(Uuid tenantId, Uuid programId)
        : base(programId, new EventStreamAddress(tenantId.ToString(), "work-assignments",
            programId.ToString()))
    {
        _tenantId = tenantId;
        On<WorkItemAssigned>(ev =>
        {
            var entry = Get(ev.WorkItemId);
            entry.Revision = ev.Revision;
            entry.AssigneeMemberId = ev.AssigneeMemberId;
            entry.Escalated = false;
            entry.History.Add(new WorkAssignmentEntryView(ev.Revision, ev.Action,
                ev.AssigneeMemberId, ev.PreviousAssigneeMemberId, ev.Reason, ev.AssignedBy,
                ev.AssignedAt));
        });
        On<WorkItemEscalated>(ev =>
        {
            var entry = Get(ev.WorkItemId);
            entry.Revision = ev.Revision;
            entry.Escalated = true;
            entry.History.Add(new WorkAssignmentEntryView(ev.Revision, Escalate, null, null,
                ev.Reason, ev.EscalatedBy, ev.EscalatedAt));
        });
    }

    /// <summary>The recorded accountability for a work item, or an empty entry at revision 0.</summary>
    public WorkAssignmentState Read(Uuid workItemId) =>
        _items.TryGetValue(workItemId, out var entry)
            ? new WorkAssignmentState(entry.Revision, entry.AssigneeMemberId, entry.Escalated,
                entry.History.ToArray())
            : new WorkAssignmentState(0, null, false, []);

    public CommandFailure? AssignTo(Uuid workItemId, long expectedRevision, string action,
        Uuid assigneeMemberId, Uuid? previousAssigneeMemberId, string? reason,
        ActorReference actor, DateTimeOffset at)
    {
        if (Stale(workItemId, expectedRevision) is { } stale)
            return stale;
        if (reason is { } text && text.Trim().Length > MaximumReasonLength)
            return CommandFailure.InvalidContent("A reason must be at most 2000 characters.");
        RaiseEvent(new WorkItemAssigned(_tenantId, Id, workItemId, expectedRevision + 1, action,
            assigneeMemberId, previousAssigneeMemberId,
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), actor, at));
        return null;
    }

    public CommandFailure? EscalateItem(Uuid workItemId, long expectedRevision, string reason,
        ActorReference actor, DateTimeOffset at)
    {
        if (Stale(workItemId, expectedRevision) is { } stale)
            return stale;
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > MaximumReasonLength)
            return CommandFailure.InvalidContent(
                "An escalation needs a reason of at most 2000 characters.");
        if (Read(workItemId).Escalated)
            return CommandFailure.StateConflict("The work item is already escalated.");
        RaiseEvent(new WorkItemEscalated(_tenantId, Id, workItemId, expectedRevision + 1,
            reason.Trim(), actor, at));
        return null;
    }

    CommandFailure? Stale(Uuid workItemId, long expectedRevision)
    {
        var current = Read(workItemId).Revision;
        return expectedRevision == current
            ? null
            : CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("work item", current));
    }

    Entry Get(Uuid workItemId)
    {
        if (!_items.TryGetValue(workItemId, out var entry))
            _items[workItemId] = entry = new Entry();
        return entry;
    }

    sealed class Entry
    {
        public long Revision { get; set; }
        public Uuid? AssigneeMemberId { get; set; }
        public bool Escalated { get; set; }
        public List<WorkAssignmentEntryView> History { get; } = [];
    }
}
