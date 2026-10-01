using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Raises M0-D10 method-change reassessment triggers when a later risk method version is
///     published. The first version has no predecessor, so it triggers nothing.
/// </summary>
public sealed partial class RiskMethodReassessmentReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("risk-methods"),
            "RiskMethodReassessmentV1"),
      IReactorHandler<RiskMethodVersionPublished>
{
    public async ValueTask HandleAsync(IReactorContext<RiskMethodVersionPublished> context,
        CancellationToken ct)
    {
        var version = context.Trigger.Version;
        if (version.Version <= 1)
            return;
        await bus.SendReactionAsync(new RaiseRiskReassessmentTriggers(context.Trigger.TenantId,
                context.Trigger.ProgramId, RiskGovernanceLedger.MethodChanged,
                version.MethodVersionId.ToString(), version.PublishedAt, version.MethodVersionId),
            context, ct).ConfigureAwait(false);
    }
}
