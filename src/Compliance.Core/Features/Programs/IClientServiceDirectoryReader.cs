using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public interface IClientServiceDirectoryReader
{
    ValueTask<ClientServiceView?> GetAsync(Uuid tenantId, Uuid serviceId, CancellationToken ct = default);
    ValueTask<Page<ClientServiceView>> ListAsync(Uuid tenantId, int limit, string? cursor,
        CancellationToken ct = default);
    ValueTask<Page<ClientServiceView>> ListProgramAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default);
    ValueTask<Page<ClientServiceRevisionView>?> ListRevisionsAsync(Uuid tenantId, Uuid serviceId,
        int limit, string? cursor, CancellationToken ct = default);
    ValueTask<ClientServiceRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid serviceId,
        long revision, CancellationToken ct = default);
}
