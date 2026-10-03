using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Reads the program-scoped source records the readiness rules evaluate.</summary>
public interface IReadinessSourceReader
{
    ValueTask<Result<ReadinessSourceSet>> ReadAsync(Uuid tenantId, Uuid programId,
        DateTimeOffset asOf, CancellationToken ct = default);
}
