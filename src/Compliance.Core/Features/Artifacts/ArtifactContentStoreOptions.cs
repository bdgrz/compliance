using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Features.Artifacts;

/// <summary>
/// Settings for the local development content adapter.
/// </summary>
public sealed record ArtifactContentStoreOptions(string? LocalRoot, byte[]? DeliveryKey, long MaxContentLength)
{
    /// <summary>
    /// Reads <c>Artifacts:LocalContentRoot</c> and the base64 <c>Artifacts:LocalDeliveryKey</c>. Missing values leave
    /// the adapter failing closed when used; a malformed key fails composition.
    /// </summary>
    public static ArtifactContentStoreOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
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
        }

        return new ArtifactContentStoreOptions(configuration["Artifacts:LocalContentRoot"], key,
            ArtifactContentLimits.MaxContentLength);
    }
}
