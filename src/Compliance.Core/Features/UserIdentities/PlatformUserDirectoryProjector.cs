using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed partial class PlatformUserDirectoryProjector(IPlatformUserDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForPattern("bdgrz", "user-identities"),
        "PlatformUserDirectory"),
      IProjectorHandler<UserIdentityRegistered>
{
    public ValueTask HandleAsync(UserIdentityRegistered ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
