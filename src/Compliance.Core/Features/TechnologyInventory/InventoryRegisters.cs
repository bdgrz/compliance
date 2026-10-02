using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     Locations and operational processes each live in one tenant register stream so that
///     active-name uniqueness is decided inside a single consistency boundary.
/// </summary>
static class InventoryRegisters
{
    public const string Area = "inventory-registers";
    public const string Locations = "locations";
    public const string Processes = "operational-processes";
    public const string ManualSource = "manual";

    public static EventStreamAddress Address(Uuid tenantId, string register) =>
        new(tenantId.ToString(), Area, register);

    public static EventStreamPattern TenantPattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), Area);
}
