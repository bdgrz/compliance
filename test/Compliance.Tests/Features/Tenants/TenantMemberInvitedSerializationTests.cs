using System.Text.Json;
using System.Text.Json.Nodes;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantMemberInvitedSerializationTests
{
    [Fact]
    public void ShouldReplayHistoricalInvitationGivenMissingDeliveryFields()
    {
        // Arrange
        var original = new TenantMemberInvited(Uuid.CreateVersion4(), "member@example.com",
            "client_personnel", false, new string('A', 64), DateTimeOffset.UtcNow.AddDays(7),
            Uuid.CreateVersion4());
        var json = JsonSerializer.Serialize(original,
            ComplianceCoreJsonContext.Default.TenantMemberInvited);
        var document = JsonNode.Parse(json)!.AsObject();
        Assert.True(document.Remove("delivery_attempt_id"));
        Assert.True(document.Remove("token_key_id"));

        // Act
        var replayed = JsonSerializer.Deserialize(document.ToJsonString(),
            ComplianceCoreJsonContext.Default.TenantMemberInvited);

        // Assert
        Assert.NotNull(replayed);
        Assert.Equal(original, replayed);
        Assert.Null(replayed.DeliveryAttemptId);
        Assert.Null(replayed.TokenKeyId);
        Assert.NotEqual(Uuid.Empty, TenantInvitation.DeliveryAttemptFor(replayed));
    }
}
