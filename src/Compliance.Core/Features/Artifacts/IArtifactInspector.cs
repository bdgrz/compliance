namespace Bdgrz.Compliance.Features.Artifacts;

/// <summary>
/// Inspection hook for stored content, such as validation or malware scanning.
/// </summary>
public interface IArtifactInspector
{
    ValueTask<ArtifactInspectionResult> InspectAsync(ArtifactContentReference content,
        CancellationToken ct = default);
}
