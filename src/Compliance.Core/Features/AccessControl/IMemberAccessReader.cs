using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public interface IMemberAccessReader
{
    ValueTask<IReadOnlyList<MemberAccessEdge>> ReadAsync(Uuid tenantId, Uuid memberId,
        CancellationToken ct = default);
}
