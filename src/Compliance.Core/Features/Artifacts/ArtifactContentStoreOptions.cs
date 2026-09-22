using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Features.Artifacts;

/// <summary>
/// Settings for the local development content adapter.
/// </summary>
public sealed record ArtifactContentStoreOptions(string? LocalRoot, byte[]? DeliveryKey, long MaxContentLength)
{
    /// <summary>The shortest accepted delivery signing key.</summary>
    public const int MinimumDeliveryKeyLength = 32;

    /// <summary>
    /// Reads the absolute <c>Artifacts:LocalContentRoot</c> and the base64 <c>Artifacts:LocalDeliveryKey</c>.
    /// Missing values leave the adapter failing closed when used. A relative root or an invalid key fails composition,
    /// so split API and worker hosts cannot resolve different directories or start with an unusable key.
    /// </summary>
    public static ArtifactContentStoreOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var root = configuration["Artifacts:LocalContentRoot"];
        if (!string.IsNullOrEmpty(root) && !Path.IsPathFullyQualified(root))
            throw new InvalidOperationException("Artifacts:LocalContentRoot must be an absolute path.");

        var encodedKey = configuration["Artifacts:LocalDeliveryKey"];
        byte[]? key = null;
        if (!string.IsNullOrEmpty(encodedKey))
        {
            try
            {
                key = Convert.FromBase64String(encodedKey);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException("Artifacts:LocalDeliveryKey must be base64.", exception);
            }

            if (key.Length < MinimumDeliveryKeyLength)
                throw new InvalidOperationException(
                    $"Artifacts:LocalDeliveryKey must decode to at least {MinimumDeliveryKeyLength} bytes.");
        }

        return new ArtifactContentStoreOptions(root, key, ArtifactContentLimits.MaxContentLength);
    }
}
