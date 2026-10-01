using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

public sealed record CampaignAmendmentView(long Reconciliation, Uuid PersonId,
    string DisplayName, string Reason, DateOnly? DueOn, Uuid RosterSnapshotId,
    string RosterContentSha256, ActorReference Actor, DateTimeOffset AmendedAt);
