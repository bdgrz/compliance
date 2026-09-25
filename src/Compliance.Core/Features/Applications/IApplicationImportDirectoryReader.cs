using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationImportDirectoryReader
{
    ValueTask<ApplicationImportView?> GetAsync(Uuid tenantId, Uuid batchId,
        CancellationToken ct = default);
    ValueTask<Page<ApplicationImportRowView>> ListRowsAsync(Uuid tenantId, Uuid batchId,
        int limit, string? cursor, CancellationToken ct = default);
}
