using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed partial class ProgramDirectoryProjector(IProgramDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "ProgramDirectory"),
      IProjectorHandler<ProgramCreated>, IProjectorHandler<ProgramRevised>,
      IProjectorHandler<ProgramCriteriaEditionSelected>
{
    public ValueTask HandleAsync(ProgramCreated ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ProgramRevised ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ProgramCriteriaEditionSelected ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
