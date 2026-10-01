namespace Bdgrz.Compliance.Features.Work;

/// <summary>One visible item with the actor's relationship to it.</summary>
sealed record WorkQueueEntry(WorkCandidate Candidate, WorkAssignmentState State,
    WorkQueueItemView Item, bool ActorEligible, bool ActorInTeam);
