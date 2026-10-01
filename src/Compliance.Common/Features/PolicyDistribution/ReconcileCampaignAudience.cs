using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Compares a later frozen roster snapshot with the campaign population and records joiner,
///     mover, and leaver amendments. It never changes platform or provider access.
/// </summary>
[Discriminator("bdgrz.campaign.audience.reconcile", 1)]
public sealed record ReconcileCampaignAudience(Uuid TenantId, Uuid ProgramId, Uuid CampaignId,
    Uuid RosterSnapshotId)
    : IRequest<CampaignReconciliationView>, IProgramScopedRequest, ICallable;
