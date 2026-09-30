using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Components, information assets, and data flows share one tenant stream area.</summary>
static class TechnologyInventoryStreams
{
    public const string Area = "technology-inventory";
    public const string ManualSource = "manual";

    public static EventStreamAddress Address(Uuid tenantId, Uuid recordId) =>
        new(tenantId.ToString(), Area, recordId.ToString());

    public static EventStreamPattern TenantPattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), Area);
}
