using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>One attributed claim, assign, reassign, delegate, or escalate action.</summary>
public sealed record WorkAssignmentEntryView(long Revision, string Action,
    Uuid? AssigneeMemberId, Uuid? PreviousAssigneeMemberId, string? Reason, ActorReference By,
    DateTimeOffset At);
