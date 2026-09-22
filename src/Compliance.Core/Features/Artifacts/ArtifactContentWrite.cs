namespace Bdgrz.Compliance.Features.Artifacts;

/// <summary>
/// The result of a conditional content write. <see cref="AlreadyPresent" /> is true only after the stored bytes verify.
/// </summary>
public sealed record ArtifactContentWrite(ArtifactContentReference Content, bool AlreadyPresent);
