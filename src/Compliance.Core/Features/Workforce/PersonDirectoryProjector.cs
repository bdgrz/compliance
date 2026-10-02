using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed partial class PersonDirectoryProjector(IPersonDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("people"), "PersonDirectoryV2"),
      IProjectorHandler<PersonRecorded>, IProjectorHandler<PersonRevised>,
      IProjectorHandler<PersonMembershipCorrelated>
{
    public ValueTask HandleAsync(PersonRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(PersonRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(PersonMembershipCorrelated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
