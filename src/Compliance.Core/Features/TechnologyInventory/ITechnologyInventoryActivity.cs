using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Answers whether a governed boundary subject is an active inventory record.</summary>
public interface ITechnologyInventoryActivity
{
    ValueTask<bool> IsActiveAsync(Uuid tenantId, string subjectType, Uuid id,
        CancellationToken ct = default);
}
