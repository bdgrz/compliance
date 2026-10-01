using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public interface IWorkforceObservationResolutionReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
    /// <summary>Returns the closures that exist among the given observations of one tenant.</summary>
    ValueTask<IReadOnlyDictionary<Uuid, WorkforceObservationResolutionView>> GetManyAsync(
        Uuid tenantId, IReadOnlyCollection<Uuid> observationIds, CancellationToken ct = default);
}
