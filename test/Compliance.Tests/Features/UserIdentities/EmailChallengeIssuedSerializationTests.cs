using System.Text.Json;
using System.Text.Json.Nodes;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class EmailChallengeIssuedSerializationTests
{
    [Fact]
    public void ShouldReplayLegacyEventGivenMissingTokenKeyId()
    {
        // Arrange
        var original = new EmailChallengeIssued(Uuid.CreateVersion4(), "owner@example.com",
            Uuid.CreateVersion4(), new string('A', 64), DateTimeOffset.UtcNow.AddMinutes(15));
        var json = JsonSerializer.Serialize(original,
            ComplianceCoreJsonContext.Default.EmailChallengeIssued);
        var document = JsonNode.Parse(json)!.AsObject();
        Assert.True(document.Remove("token_key_id"));

        // Act
        var replayed = JsonSerializer.Deserialize(document.ToJsonString(),
            ComplianceCoreJsonContext.Default.EmailChallengeIssued);

        // Assert
        Assert.NotNull(replayed);
        Assert.Equal(original, replayed);
        Assert.Null(replayed.TokenKeyId);
    }
}
