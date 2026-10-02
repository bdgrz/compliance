using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     A governed place relevant to the scoped system, such as a physical site or hosting
///     region (M0-D22). Kind and name together identify it among active locations.
/// </summary>
public sealed record LocationContent(string Kind, string Name, string? GeographyReference,
    Uuid OwnerPersonId, string Lifecycle);
