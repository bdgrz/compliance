using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class SmtpTenantInvitationDeliveryTests
{
    [Fact]
    public async Task ShouldHideRecipientAndTokenGivenMalformedRecipient()
    {
        // Arrange
        var settings = new EmailChallengeDeliverySettings("smtp", "smtp.example.com", 587,
            "sender@example.com", null, null);
        var delivery = new SmtpTenantInvitationDelivery(settings);
        const string recipient = "not-an-address";
        const string token = "private-invitation-token";

        // Act
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await delivery.SendAsync(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                recipient, token, CancellationToken.None));

        // Assert
        Assert.Equal("Invitation delivery failed.", failure.Message);
        Assert.DoesNotContain(recipient, failure.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(token, failure.ToString(), StringComparison.Ordinal);
    }
}
