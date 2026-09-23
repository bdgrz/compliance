using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Derives a challenge token without persisting its plaintext.</summary>
public sealed class EmailChallengeTokenKeys
{
    const string Section = "Compliance:EmailDelivery";
    static readonly byte[] DevelopmentKey = SHA256.HashData(
        Encoding.UTF8.GetBytes("bdgrz-development-email-challenge-key-v1"));
    readonly Dictionary<string, byte[]> _keys;

    EmailChallengeTokenKeys(string activeKeyId, Dictionary<string, byte[]> keys)
    {
        ActiveKeyId = activeKeyId;
        _keys = keys;
    }

    public string ActiveKeyId { get; }

    public static EmailChallengeTokenKeys FromConfiguration(IConfiguration configuration,
        bool allowDevelopmentKey)
    {
        var active = configuration[$"{Section}:ActiveTokenKeyId"];
        var keys = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in configuration.GetSection($"{Section}:TokenKeys").GetChildren())
        {
            if (entry.Value is null)
                continue;
            byte[] decoded;
            try
            {
                decoded = Convert.FromBase64String(entry.Value);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("An email delivery token key is invalid.");
            }
            if (decoded.Length < 32)
                throw new InvalidOperationException("Email delivery token keys must contain at least 32 bytes.");
            keys.Add(entry.Key, decoded);
        }

        if (keys.Count == 0 && allowDevelopmentKey && active is null)
        {
            keys.Add("development", DevelopmentKey);
            active = "development";
        }
        if (string.IsNullOrWhiteSpace(active) || !keys.ContainsKey(active))
            throw new InvalidOperationException("The active email delivery token key is unavailable.");
        return new EmailChallengeTokenKeys(active, keys);
    }

    public string Derive(string keyId, Uuid challengeId, Uuid userId,
        string emailAddress, DateTimeOffset expiresAt) =>
        TryDerive(keyId, challengeId, userId, emailAddress, expiresAt, out var token)
            ? token!
            : throw new InvalidOperationException("The email delivery token key is unavailable.");

    public bool TryDerive(string? keyId, Uuid challengeId, Uuid userId,
        string emailAddress, DateTimeOffset expiresAt, out string? token)
    {
        token = null;
        if (keyId is null || !_keys.TryGetValue(keyId, out var key))
            return false;

        // The random challenge ID provides unique input. The HMAC key is shared by API and
        // worker; retaining the previous key for 15 minutes permits in-flight delivery.
        var input = Encoding.UTF8.GetBytes(string.Join('\n',
            "bdgrz-email-challenge-v1", challengeId.ToString(), userId.ToString(),
            emailAddress, expiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
        token = Convert.ToHexString(HMACSHA256.HashData(key, input));
        return true;
    }
}
