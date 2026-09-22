namespace Bdgrz.Compliance.Features.Artifacts;

/// <summary>
/// The storage-level inspection state. Only a selected inspection engine may report a state other than
/// <see cref="NotInspected" />.
/// </summary>
public enum ArtifactInspectionState
{
    NotInspected,
    Clean,
    Quarantined,
}
