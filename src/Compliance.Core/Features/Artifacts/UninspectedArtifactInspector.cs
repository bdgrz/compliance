namespace Bdgrz.Compliance.Features.Artifacts;

/// <summary>
/// The default inspector. It never reports content as clean, because no inspection engine is selected.
/// </summary>
public sealed class UninspectedArtifactInspector : IArtifactInspector
{
    public ValueTask<ArtifactInspectionResult> InspectAsync(ArtifactContentReference content,
        CancellationToken ct = default) =>
        ValueTask.FromResult(new ArtifactInspectionResult(ArtifactInspectionState.NotInspected));
}
