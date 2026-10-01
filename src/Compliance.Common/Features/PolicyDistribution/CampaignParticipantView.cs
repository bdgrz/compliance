using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     One person in the evidence population as of a date. <c>Inclusion</c> is <c>launch</c>,
///     <c>joiner</c>, or <c>mover_in</c>; <c>State</c> is <c>pending</c>, <c>overdue</c>,
///     <c>acknowledged</c> (policy), <c>completed</c> (training), <c>excepted</c>, or
///     <c>removed</c>. Earlier results remain after removal.
/// </summary>
public sealed record CampaignParticipantView(Uuid PersonId, string DisplayName,
    string WorkerType, string? Department, string Inclusion, Uuid IncludedBySnapshotId,
    string State, DateOnly DueOn, string? RemovedReason, Uuid? RemovedBySnapshotId,
    CampaignAcknowledgementView? Acknowledgement, CampaignCompletionView? Completion,
    CampaignWaiverView? Waiver);
