using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed record FirmProfessionalAuthorityTrustSettings(string Issuer, string Audience, string KeyId,
    string PublicKeyBase64, int MaximumAssertionLifetimeSeconds)
{
    const string ConfigurationPath = "Compliance:ProfessionalAuthority";
    const int MaximumAllowedLifetimeSeconds = 86_400;

    public static FirmProfessionalAuthorityTrustSettings? FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationPath);
        var issuer = section["Issuer"];
        var audience = section["Audience"];
        var keyId = section["KeyId"];
        var publicKeyBase64 = section["PublicKeyBase64"];
        var maximumLifetimeText = section["MaximumAssertionLifetimeSeconds"];
        int? maximumLifetime = int.TryParse(maximumLifetimeText, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var parsedMaximumLifetime)
            ? parsedMaximumLifetime
            : null;
        if (maximumLifetimeText is not null && maximumLifetime is null)
            throw new InvalidOperationException("Professional authority assertion lifetime must be an integer.");
        if (string.IsNullOrWhiteSpace(issuer) && string.IsNullOrWhiteSpace(audience) &&
            string.IsNullOrWhiteSpace(keyId) && string.IsNullOrWhiteSpace(publicKeyBase64) &&
            maximumLifetime is null)
            return null;

        if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri) ||
            issuerUri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(audience) || audience.Length > 200 ||
            audience.Any(char.IsControl) || string.IsNullOrWhiteSpace(keyId) || keyId.Length > 200 ||
            keyId.Any(char.IsControl) || string.IsNullOrWhiteSpace(publicKeyBase64) ||
            publicKeyBase64.Length > 16_384 || maximumLifetime is not > 0 ||
            maximumLifetime > MaximumAllowedLifetimeSeconds)
            throw new InvalidOperationException(
                "Professional authority trust requires an HTTPS issuer, exact audience, key ID, public key and bounded assertion lifetime.");

        byte[] publicKey;
        try
        {
            publicKey = Convert.FromBase64String(publicKeyBase64);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "Professional authority PublicKeyBase64 must contain a valid SubjectPublicKeyInfo RSA key.", exception);
        }

        using var rsa = System.Security.Cryptography.RSA.Create();
        try
        {
            rsa.ImportSubjectPublicKeyInfo(publicKey, out var bytesRead);
            if (bytesRead != publicKey.Length || rsa.KeySize is < 2048 or > 4096 ||
                Convert.ToBase64String(publicKey) != publicKeyBase64.Trim())
                throw new InvalidOperationException("Professional authority keys must be canonical complete RSA keys from 2048 to 4096 bits.");
        }
        catch (System.Security.Cryptography.CryptographicException exception)
        {
            throw new InvalidOperationException(
                "Professional authority PublicKeyBase64 must contain a valid SubjectPublicKeyInfo RSA key.", exception);
        }

        return new FirmProfessionalAuthorityTrustSettings(issuerUri.AbsoluteUri.TrimEnd('/'), audience.Trim(),
            keyId.Trim(), publicKeyBase64.Trim(), maximumLifetime.Value);
    }
}
