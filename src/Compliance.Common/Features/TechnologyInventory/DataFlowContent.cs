using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     A flow from a component or system instance to a data store component or an external
///     party (M0-D08). <see cref="Classification" /> is the highest classification carried
///     when the version was recorded.
/// </summary>
public sealed record DataFlowContent(string SourceType, Uuid SourceId, string DestinationType,
    Uuid? DestinationId, string? DestinationParty, IReadOnlyList<Uuid> InformationAssetIds,
    string Purpose, bool EncryptedInTransit, bool EncryptedAtRest, string? ExceptionReference,
    DateOnly EffectiveFrom, Uuid OwnerPersonId, string Lifecycle, string Classification);
