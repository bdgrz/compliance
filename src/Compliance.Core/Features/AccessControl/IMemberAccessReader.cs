using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record MemberAccessEdge(Uuid TeamId, Uuid RoleId,
    IReadOnlyList<string> Permissions);

public interface IMemberAccessReader
{
    ValueTask<IReadOnlyList<MemberAccessEdge>> ReadAsync(Uuid tenantId, Uuid memberId,
        CancellationToken ct = default);
}
