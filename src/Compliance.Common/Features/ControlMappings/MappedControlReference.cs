using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     An accepted mapping from an exact control version to one catalog entry.
///     <c>RemapRequired</c> is true when that version is no longer the control's current approved
///     version, such as after a successor or retirement; the mapping is never moved silently.
/// </summary>
public sealed record MappedControlReference(Uuid MappingId, Uuid ControlId,
    Uuid ControlVersionId, int VersionNumber, string ApplicabilityExplanation)
{
    public bool RemapRequired { get; init; }
}
