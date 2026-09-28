using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public interface IUserIdentityDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(CancellationToken ct = default);

    ValueTask<UserIdentityDirectoryPage> ListAsync(Uuid userId, int limit,
        string? cursor, CancellationToken ct = default);
}
