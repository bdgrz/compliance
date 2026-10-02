using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed partial class PlatformUserDirectoryProjector(IPlatformUserDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForPattern("bdgrz", "user-identities"),
        "PlatformUserDirectoryV2"),
      IProjectorHandler<UserIdentityRegistered>, IProjectorHandler<UserIdentityProfileObserved>
{
    public ValueTask HandleAsync(UserIdentityRegistered ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(UserIdentityProfileObserved ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
