using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Records a governed operational process in the tenant's inventory.</summary>
[Discriminator("bdgrz.inventory.operational_process.record", 1)]
public sealed record RecordOperationalProcess(Uuid TenantId, string Name, string Purpose,
    Uuid OwnerPersonId, string? Inputs = null, string? Outputs = null)
    : IRequest<OperationalProcessRegistration>, ITechnologyInventoryWriteRequest, ICallable;
