using Bdgrz.Compliance.Features.Applications;
using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationGovernedRevisionGateTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("1", false)]
    [InlineData("invalid", false)]
    public void ShouldEnableOnlyExplicitTrueGivenIndependentRevisionConfiguration(string? configured,
        bool expected)
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Compliance:Applications:GovernedRevisionsEnabled"] = configured,
                ["Compliance:Applications:GovernedDeclarationsEnabled"] = (!expected).ToString(),
            }).Build();

        // Act
        var gate = ApplicationGovernedRevisionGate.FromConfiguration(configuration);

        // Assert
        Assert.Equal(expected, gate.IsEnabled);
    }
}
