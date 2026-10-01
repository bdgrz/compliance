using Bdgrz.Compliance.Features.Policies;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Reconciles a campaign against a roster snapshot frozen after the one it last used. The
///     previous snapshot explains whether an addition is a joiner or a mover.
/// </summary>
public sealed class ReconcileCampaignAudienceHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<ReconcileCampaignAudience, CampaignReconciliationView>
{
    public async ValueTask<Result<CampaignReconciliationView>> HandleAsync(
        IRequestContext<ReconcileCampaignAudience> context, CancellationToken ct)
    {
        var request = context.Request;
        var source = await CampaignSource.ReadAsync(reader, request.TenantId, request.ProgramId,
            request.CampaignId, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<CampaignReconciliationView>.Failure(source.Error);
        var campaign = source.Value;
        var previousId = campaign.LatestRosterSnapshotId;
        var previous = await RosterSnapshotSource.ReadAsync(reader, request.TenantId,
            previousId, ct).ConfigureAwait(false);
        if (!previous.IsSuccess)
            return Result<CampaignReconciliationView>.Failure(previous.Error);
        var current = await RosterSnapshotSource.ReadAsync(reader, request.TenantId,
            request.RosterSnapshotId, ct).ConfigureAwait(false);
        if (!current.IsSuccess)
            return Result<CampaignReconciliationView>.Failure(current.Error);
        if (!campaign.HasReconciled(request.RosterSnapshotId) &&
            current.Value.FrozenAt <= previous.Value.FrozenAt)
            return Result<CampaignReconciliationView>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "Reconciliation requires a roster snapshot frozen after the campaign's latest one."));
        var audience = RosterAudience.Evaluate(current.Value.Roster, campaign.AudienceKind,
            campaign.AudienceTeams);
        var previouslyPresent = RosterAudience.Evaluate(previous.Value.Roster,
            campaign.AudienceKind, campaign.AudienceTeams).Present;
        var actor = PolicyActor.From(context, request.TenantId);
        return await executor.ExecuteAsync(new PolicyDistributionCampaign(request.TenantId,
                request.CampaignId),
            target => target.LatestRosterSnapshotId != previousId
                ? AggregateOutcome.Discard(Result<CampaignReconciliationView>.Failure(
                    new RequestError(RequestErrorKind.Conflict,
                        "The campaign was reconciled concurrently. Retry against its latest snapshot.")))
                : AggregateOutcome.CommitOnSuccess(target.Reconcile(request.ProgramId,
                    request.RosterSnapshotId, current.Value.Roster.ContentSha256, audience,
                    previouslyPresent, actor.Reference, clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
