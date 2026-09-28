using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed partial class IdentityDirectoryProjector(IUserIdentityDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForPattern("bdgrz", "user-identities"),
        "UserIdentityDirectory"),
      IProjectorHandler<UserIdentityRegistered>,
      IProjectorHandler<UserIdentityAuthenticated>,
      IProjectorHandler<UserIdentityRevoked>
{
    public ValueTask HandleAsync(UserIdentityRegistered ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(UserIdentityAuthenticated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(UserIdentityRevoked ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
