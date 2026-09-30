using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>M0-D08 vocabularies and record-local validation for the manual inventory.</summary>
public static class TechnologyInventoryRules
{
    public const string Active = "active";
    public const string Retired = "retired";
    public const string CloudAccount = "cloud_account";
    public const string DataStore = "data_store";
    public const string EndpointClass = "endpoint_class";
    public const string ComponentReference = "technology_component";
    public const string SystemInstanceReference = "system_instance";
    public const string ExternalPartyReference = "external_party";

    static readonly string[] Categories =
        [CloudAccount, "environment", "network", DataStore, "repository", EndpointClass];

    static readonly string[] Classifications = ["public", "internal", "confidential", "restricted"];

    public static bool IsClassification(string? value) =>
        value is not null && Classifications.Contains(value, StringComparer.Ordinal);

    /// <summary>Orders classifications from least to most sensitive; unknown values rank lowest.</summary>
    public static int Rank(string classification) =>
        Array.IndexOf(Classifications, classification);

    public static bool RequiresEncryption(string classification) =>
        Rank(classification) >= Rank("confidential");

    public static TechnologyComponentContent Normalize(TechnologyComponentContent content) =>
        content with
        {
            Category = content.Category?.Trim() ?? string.Empty,
            Name = content.Name?.Trim() ?? string.Empty,
            EnvironmentReference = Optional(content.EnvironmentReference),
            LocationReference = Optional(content.LocationReference),
            ManagementSource = Optional(content.ManagementSource),
            Lifecycle = content.Lifecycle?.Trim() ?? string.Empty,
        };

    public static string? Validate(TechnologyComponentContent content)
    {
        if (!Categories.Contains(content.Category, StringComparer.Ordinal))
            return "The component category must be cloud_account, environment, network, data_store, repository, or endpoint_class.";
        if (content.Name.Length is < 1 or > 200)
            return "A component requires a name of at most 200 characters.";
        if (content.OwnerPersonId == Uuid.Empty)
            return "A component requires an owner.";
        if (content.EnvironmentReference is { Length: > 200 } ||
            content.LocationReference is { Length: > 200 })
            return "Environment and location references must be at most 200 characters.";
        if ((content.Category == CloudAccount) != content.SystemInstanceId is { } instanceId ||
            content.SystemInstanceId == Uuid.Empty)
            return "Exactly the cloud_account category references its system instance.";
        if (content.Category == EndpointClass
                ? content.EndpointCount is not >= 0 || content.ManagementSource is null ||
                  content.ManagementSource.Length > 200
                : content.EndpointCount is not null || content.ManagementSource is not null)
            return "Exactly the endpoint_class category records a count and management source.";
        return LifecycleError(content.Lifecycle);
    }

    public static InformationAssetContent Normalize(InformationAssetContent content) =>
        content with
        {
            Name = content.Name?.Trim() ?? string.Empty,
            Classification = content.Classification?.Trim() ?? string.Empty,
            RetentionReference = content.RetentionReference?.Trim() ?? string.Empty,
            Description = Optional(content.Description),
            Lifecycle = content.Lifecycle?.Trim() ?? string.Empty,
        };

    public static string? Validate(InformationAssetContent content)
    {
        if (content.Name.Length is < 1 or > 200)
            return "An information asset requires a name of at most 200 characters.";
        if (!IsClassification(content.Classification))
            return "The classification must be public, internal, confidential, or restricted.";
        if (content.RetentionReference.Length is < 1 or > 200)
            return "An information asset requires a retention reference of at most 200 characters.";
        if (content.OwnerPersonId == Uuid.Empty)
            return "An information asset requires an owner.";
        if (content.Description is { Length: > 2000 })
            return "The description must be at most 2000 characters.";
        return LifecycleError(content.Lifecycle);
    }

    public static DataFlowContent Normalize(DataFlowContent content) =>
        content with
        {
            SourceType = content.SourceType?.Trim() ?? string.Empty,
            DestinationType = content.DestinationType?.Trim() ?? string.Empty,
            DestinationParty = Optional(content.DestinationParty),
            InformationAssetIds = content.InformationAssetIds ?? [],
            Purpose = content.Purpose?.Trim() ?? string.Empty,
            ExceptionReference = Optional(content.ExceptionReference),
            Lifecycle = content.Lifecycle?.Trim() ?? string.Empty,
            Classification = content.Classification?.Trim() ?? string.Empty,
        };

    public static string? Validate(DataFlowContent content)
    {
        if (content.SourceType is not (ComponentReference or SystemInstanceReference) ||
            content.SourceId == Uuid.Empty)
            return "A data flow source must be a technology_component or system_instance.";
        if (content.DestinationType switch
        {
            ComponentReference => content.DestinationId is not { } id || id == Uuid.Empty ||
                                  content.DestinationParty is not null,
            ExternalPartyReference => content.DestinationId is not null ||
                                      content.DestinationParty is not { Length: <= 200 },
            _ => true,
        })
            return "A data flow destination must be a data store component or a named external party.";
        if (content.InformationAssetIds.Count is < 1 or > 100 ||
            content.InformationAssetIds.Any(static id => id == Uuid.Empty) ||
            content.InformationAssetIds.Distinct().Count() != content.InformationAssetIds.Count)
            return "A data flow carries between 1 and 100 distinct information assets.";
        if (content.Purpose.Length is < 1 or > 1000)
            return "A data flow requires a purpose of at most 1000 characters.";
        if (content.ExceptionReference is { Length: > 200 })
            return "The exception reference must be at most 200 characters.";
        if (content.OwnerPersonId == Uuid.Empty)
            return "A data flow requires an owner.";
        if (!IsClassification(content.Classification))
            return "The carried classification is unknown.";
        if (RequiresEncryption(content.Classification) &&
            !(content.EncryptedInTransit && content.EncryptedAtRest) &&
            content.ExceptionReference is null)
            return "A flow carrying confidential or restricted information requires encryption in transit and at rest, or an approved exception reference.";
        return LifecycleError(content.Lifecycle);
    }

    public static bool SameContent(DataFlowContent left, DataFlowContent right) =>
        left with { InformationAssetIds = [] } == right with { InformationAssetIds = [] } &&
        left.InformationAssetIds.SequenceEqual(right.InformationAssetIds);

    static string? LifecycleError(string lifecycle) =>
        lifecycle is Active or Retired ? null : "The lifecycle must be active or retired.";

    static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
