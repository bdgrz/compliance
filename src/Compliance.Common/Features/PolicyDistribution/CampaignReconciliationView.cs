using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

public sealed record CampaignReconciliationView(Uuid CampaignId, long Reconciliation,
    Uuid RosterSnapshotId, IReadOnlyList<CampaignAudienceAmendment> Amendments);
