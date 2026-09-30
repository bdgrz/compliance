using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>A governed information asset or data class with exactly one classification (M0-D08).</summary>
public sealed record InformationAssetContent(string Name, string Classification,
    string RetentionReference, Uuid OwnerPersonId, string? Description, string Lifecycle);
