using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Records a governed technology component in the tenant's manual inventory.</summary>
[Discriminator("bdgrz.inventory.component.record", 1)]
public sealed record RecordTechnologyComponent(Uuid TenantId, string Category, string Name,
    Uuid OwnerPersonId, string? EnvironmentReference = null, string? LocationReference = null,
    Uuid? SystemInstanceId = null, int? EndpointCount = null, string? ManagementSource = null)
    : IRequest<TechnologyComponentRegistration>, ITechnologyInventoryWriteRequest, ICallable;
