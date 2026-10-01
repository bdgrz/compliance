using Bdgrz.Compliance.Features.Boundaries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Raises M0-D10 boundary-change reassessment triggers when a successor boundary version is
///     approved. The initial approval defines scope rather than changing it, so it is skipped.
/// </summary>
public sealed partial class BoundaryRiskReassessmentReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus,
    IAggregateReader reader)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("boundaries"),
            "BoundaryRiskReassessmentV1"),
      IReactorHandler<BoundaryApproved>
{
    public async ValueTask HandleAsync(IReactorContext<BoundaryApproved> context,
        CancellationToken ct)
    {
        var approved = context.Trigger;
        var boundary = await reader.HydrateAsync(new SystemBoundary(approved.TenantId,
            approved.BoundaryId), ct).ConfigureAwait(false);
        if (!boundary.IsCreated || boundary.FirstApprovedVersionId == approved.DraftVersionId)
            return;
        await bus.SendReactionAsync(new RaiseRiskReassessmentTriggers(approved.TenantId,
                boundary.ProgramId, RiskGovernanceLedger.BoundaryChanged,
                approved.DraftVersionId.ToString(), approved.DecidedAt, null),
            context, ct).ConfigureAwait(false);
    }
}
