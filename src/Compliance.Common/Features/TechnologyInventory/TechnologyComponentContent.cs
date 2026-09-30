using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     One governed component at account and environment granularity (M0-D08). A
///     <c>cloud_account</c> component shares its identity with its system instance.
/// </summary>
public sealed record TechnologyComponentContent(string Category, string Name,
    Uuid OwnerPersonId, string? EnvironmentReference, string? LocationReference,
    Uuid? SystemInstanceId, int? EndpointCount, string? ManagementSource, string Lifecycle);
