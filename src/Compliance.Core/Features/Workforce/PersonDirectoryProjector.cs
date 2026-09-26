using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed partial class PersonDirectoryProjector(IPersonDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("people"), "PersonDirectoryV1"),
      IProjectorHandler<PersonRecorded>, IProjectorHandler<PersonRevised>
{
    public ValueTask HandleAsync(PersonRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(PersonRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
