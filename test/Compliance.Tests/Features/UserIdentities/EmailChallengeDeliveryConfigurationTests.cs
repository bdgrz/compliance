using System.Security.Cryptography;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.UserIdentities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class EmailChallengeDeliveryConfigurationTests
{
    [Fact]
    public void ShouldRejectMockDeliveryGivenProductionComposition()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = Configuration(null);

        // Act
        var failure = Assert.Throws<InvalidOperationException>(() =>
            services.AddCompliance(configuration, requireRealEmailDelivery: true));

        // Assert
        Assert.Contains("must be configured", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldRejectMissingKeyGivenProductionSmtpConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = Configuration(null, smtp: true);

        // Act
        var failure = Assert.Throws<InvalidOperationException>(() =>
            services.AddCompliance(configuration, requireRealEmailDelivery: true));

        // Assert
        Assert.Contains("active email delivery token key", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldRegisterSmtpGivenProductionConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var configuration = Configuration(key, smtp: true);

        // Act
        _ = services.AddCompliance(configuration, requireRealEmailDelivery: true);
        using var provider = services.BuildServiceProvider();

        // Assert
        Assert.IsType<SmtpEmailChallengeDelivery>(
            provider.GetRequiredService<IEmailChallengeDelivery>());
        Assert.IsType<SmtpTenantInvitationDelivery>(
            provider.GetRequiredService<ITenantInvitationDelivery>());
    }

    static IConfiguration Configuration(string? key, bool smtp = false)
    {
        var values = new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
            ["Compliance:EmailDelivery:Mode"] = smtp ? "smtp" : "mock",
            ["Compliance:EmailDelivery:Smtp:Host"] = "smtp.example.com",
            ["Compliance:EmailDelivery:Smtp:Port"] = "587",
            ["Compliance:EmailDelivery:Smtp:FromAddress"] = "verify@example.com",
            ["Compliance:EmailDelivery:ActiveTokenKeyId"] = key is null ? null : "current",
            ["Compliance:EmailDelivery:TokenKeys:current"] = key,
        };
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
