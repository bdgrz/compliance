using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

interface ICriteriaTextOverlayDirectoryReader
{
    ValueTask<Result<IReadOnlyDictionary<string, CriteriaTextOverlayRevision>>> GetManyAsync(
        Uuid tenantId, Uuid editionId, IReadOnlyList<string> identifiers,
        CancellationToken ct = default);

    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
}
