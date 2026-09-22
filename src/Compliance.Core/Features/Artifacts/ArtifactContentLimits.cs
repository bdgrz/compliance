namespace Bdgrz.Compliance.Features.Artifacts;

/// <summary>
/// Storage bounds accepted in ADR 0006.
/// </summary>
public static class ArtifactContentLimits
{
    /// <summary>The maximum artifact content length, 256 MiB.</summary>
    public const long MaxContentLength = 256L * 1024 * 1024;

    /// <summary>The longest lifetime of an issued delivery location.</summary>
    public static readonly TimeSpan MaxDeliveryLifetime = TimeSpan.FromMinutes(5);
}
