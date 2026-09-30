using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>An accepted mapping from an exact control version to one catalog entry.</summary>
public sealed record MappedControlReference(Uuid MappingId, Uuid ControlId,
    Uuid ControlVersionId, int VersionNumber, string ApplicabilityExplanation);
