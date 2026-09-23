using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public interface IPlatformUserDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(UserIdentityRegistered registered, CancellationToken ct = default);
}

public interface IPlatformUserDirectoryReader
{
    ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default);
}
