using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Records a governed location in the tenant's inventory.</summary>
[Discriminator("bdgrz.inventory.location.record", 1)]
public sealed record RecordLocation(Uuid TenantId, string Kind, string Name, Uuid OwnerPersonId,
    string? GeographyReference = null)
    : IRequest<LocationRegistration>, ITechnologyInventoryWriteRequest, ICallable;
