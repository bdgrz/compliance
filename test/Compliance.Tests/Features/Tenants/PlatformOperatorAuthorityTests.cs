using Cntryl.Portia;
using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class PlatformOperatorAuthorityTests
{
    [Fact]
    public void ShouldParseBootstrapSubjectsGivenProductionMode()
    {
        // Arrange
        var operatorId = Uuid.CreateVersion4();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PlatformOperators:UserIds:0"] = operatorId.ToString(),
        }).Build();

        // Act
        var authority = PlatformOperatorAuthority.FromConfiguration(configuration, developerAuthentication: false);

        // Assert
        Assert.Equal(operatorId, Assert.Single(authority.BootstrapUserIds));
        Assert.Empty(PlatformOperatorAuthority.FromConfiguration(new ConfigurationBuilder().Build(), false)
            .BootstrapUserIds);
    }

    [Fact]
    public void ShouldFailStartupGivenInvalidOperatorConfiguration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PlatformOperators:UserIds:0"] = "not-a-uuid",
        }).Build();

        // Act
        var error = Record.Exception(() =>
            PlatformOperatorAuthority.FromConfiguration(configuration, developerAuthentication: false));

        // Assert
        Assert.IsType<InvalidOperationException>(error);
    }
}
