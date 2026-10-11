using Bdgrz.Compliance.Features.Applications;
using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationGovernedDeclarationGateTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    [InlineData("invalid", false)]
    public void ShouldEnableOnlyExplicitTrueGivenDeclarationConfiguration(string? configured,
        bool expected)
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Compliance:Applications:GovernedDeclarationsEnabled"] = configured,
            }).Build();

        // Act
        var gate = ApplicationGovernedDeclarationGate.FromConfiguration(configuration);

        // Assert
        Assert.Equal(expected, gate.IsEnabled);
    }
}
