using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed partial class InventoryRegisterProjector(IInventoryRegisterProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(InventoryRegisters.Area),
            FitzInventoryRegisterDirectory.ProjectorName),
        IProjectorHandler<LocationRevisionRecorded>,
        IProjectorHandler<OperationalProcessRevisionRecorded>
{
    public ValueTask HandleAsync(LocationRevisionRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(OperationalProcessRevisionRecorded ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
