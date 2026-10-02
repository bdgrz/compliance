using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>M0-D22 vocabularies and record-local validation for locations and processes.</summary>
public static class InventoryRegisterRules
{
    public const string PhysicalSite = "physical_site";
    public const string HostingRegion = "hosting_region";
    public const string Other = "other";

    static readonly string[] LocationKinds = [PhysicalSite, HostingRegion, Other];

    public static LocationContent Normalize(LocationContent content) =>
        content with
        {
            Kind = content.Kind?.Trim() ?? string.Empty,
            Name = content.Name?.Trim() ?? string.Empty,
            GeographyReference = Optional(content.GeographyReference),
            Lifecycle = content.Lifecycle?.Trim() ?? string.Empty,
        };

    public static string? Validate(LocationContent content)
    {
        if (!LocationKinds.Contains(content.Kind, StringComparer.Ordinal))
            return "The location kind must be physical_site, hosting_region, or other.";
        if (content.Name.Length is < 1 or > 200)
            return "A location requires a name of at most 200 characters.";
        if (content.GeographyReference is { Length: > 200 })
            return "The geography reference must be at most 200 characters.";
        if (content.OwnerPersonId == Uuid.Empty)
            return "A location requires an owner.";
        return LifecycleError(content.Lifecycle);
    }

    public static OperationalProcessContent Normalize(OperationalProcessContent content) =>
        content with
        {
            Name = content.Name?.Trim() ?? string.Empty,
            Purpose = content.Purpose?.Trim() ?? string.Empty,
            Inputs = Optional(content.Inputs),
            Outputs = Optional(content.Outputs),
            Lifecycle = content.Lifecycle?.Trim() ?? string.Empty,
        };

    public static string? Validate(OperationalProcessContent content)
    {
        if (content.Name.Length is < 1 or > 200)
            return "An operational process requires a name of at most 200 characters.";
        if (content.Purpose.Length is < 1 or > 1000)
            return "An operational process requires a purpose of at most 1000 characters.";
        if (content.Inputs is { Length: > 1000 } || content.Outputs is { Length: > 1000 })
            return "Inputs and outputs must be at most 1000 characters.";
        if (content.OwnerPersonId == Uuid.Empty)
            return "An operational process requires an owner.";
        return LifecycleError(content.Lifecycle);
    }

    static string? LifecycleError(string lifecycle) =>
        lifecycle is TechnologyInventoryRules.Active or TechnologyInventoryRules.Retired
            ? null
            : "The lifecycle must be active or retired.";

    static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
