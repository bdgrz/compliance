using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public interface IUserIdentityDirectoryReadConsistency
{
    ValueTask<Result> EnsureCaughtUpAsync(CancellationToken ct);
}
