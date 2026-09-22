namespace Bdgrz.Compliance.Features.Artifacts;

/// <summary>
/// The outcome of the storage-level disposition hook.
/// </summary>
public enum ArtifactDeletionResult
{
    Deleted,
    NotFound,
    Held,
}
