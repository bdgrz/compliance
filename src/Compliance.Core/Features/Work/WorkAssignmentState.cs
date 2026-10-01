using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Recorded accountability for one work item: revision, assignee, escalation, and history.</summary>
public sealed record WorkAssignmentState(long Revision, Uuid? AssigneeMemberId, bool Escalated,
    IReadOnlyList<WorkAssignmentEntryView> History);
