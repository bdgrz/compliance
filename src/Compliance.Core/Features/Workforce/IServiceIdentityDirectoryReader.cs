using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public interface IServiceIdentityDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
    ValueTask<ServiceIdentityView?> GetAsync(Uuid tenantId, Uuid serviceIdentityId,
        CancellationToken ct = default);
    ValueTask<Page<ServiceIdentityView>> ListAsync(Uuid tenantId, int limit, string? cursor,
        CancellationToken ct = default);
}
