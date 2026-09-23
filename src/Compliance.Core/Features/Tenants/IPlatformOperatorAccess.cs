using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public interface IPlatformOperatorAccess
{
    ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default);
}
