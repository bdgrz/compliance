using System.Globalization;
using System.Security.Cryptography;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class EmailChallengeTokenKeysTests
{
    [Fact]
    public void ShouldReconstructSameTokenGivenRetainedKeyAfterRotation()
    {
        // Arrange
        var oldKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var newKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var original = CreateKeys("old", oldKey);
        var rotated = CreateKeys("new", oldKey, newKey);
        var challengeId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var expiresAt = DateTimeOffset.Parse("2026-09-23T13:15:00Z", CultureInfo.InvariantCulture);

        // Act
        var issued = original.Derive("old", challengeId, userId, "owner@example.com", expiresAt);
        var recovered = rotated.Derive("old", challengeId, userId, "owner@example.com", expiresAt);

        // Assert
        Assert.Equal(issued, recovered);
        Assert.Equal(64, issued.Length);
        Assert.NotEqual(issued, rotated.Derive("new", challengeId, userId, "owner@example.com", expiresAt));
    }

    [Fact]
    public void ShouldFailClosedGivenMissingHistoricalKey()
    {
        // Arrange
        var keys = CreateKeys("new", null, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

        // Act
        var found = keys.TryDerive("old", Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            "owner@example.com", DateTimeOffset.UtcNow.AddMinutes(15), out var token);

        // Assert
        Assert.False(found);
        Assert.Null(token);
    }

    static EmailChallengeTokenKeys CreateKeys(string active, string? oldKey, string? newKey = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Compliance:EmailDelivery:ActiveTokenKeyId"] = active,
            ["Compliance:EmailDelivery:TokenKeys:old"] = oldKey,
            ["Compliance:EmailDelivery:TokenKeys:new"] = newKey,
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return EmailChallengeTokenKeys.FromConfiguration(configuration, false);
    }
}
