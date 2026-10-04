using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Recovers verdict storage effects from tenant artifact history without rescanning or changing events.</summary>
public sealed partial class EvidenceArtifactInspectionEffectsReactor(IProjectionCheckpointStore checkpoints,
    EvidenceArtifactStorageReconciler reconciler)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(EvidenceStreams.Area), WorkloadName),
      IReactorHandler<EvidenceArtifactInspected>
{
    public const string WorkloadName = "EvidenceArtifactInspectionEffectsV1";

    public async ValueTask HandleAsync(IReactorContext<EvidenceArtifactInspected> context, CancellationToken ct)
    {
        var trigger = context.Trigger;
        if (Pattern.IsTenantTemplate || Pattern.Realm != trigger.TenantId.ToString() ||
            trigger.TenantId == Uuid.Empty || trigger.ArtifactId == Uuid.Empty ||
            context.Source.Stream != EvidenceStreams.Address(trigger.TenantId, trigger.ArtifactId))
            throw new InvalidOperationException("The evidence inspection source identity does not match its trigger.");
        var result = await reconciler.ReconcileAsync(trigger.TenantId, trigger.ArtifactId, ct)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
            throw new InvalidOperationException(result.Error!.Message);
    }
}
