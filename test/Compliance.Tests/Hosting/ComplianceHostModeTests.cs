using Bdgrz.Compliance;

namespace Bdgrz.Compliance.Tests.Hosting;

public sealed class ComplianceHostModeTests
{
    [Fact]
    public void ShouldDefaultToStandaloneGivenMissingMode()
    {
        // Arrange
        const string? value = null;

        // Act
        var mode = ComplianceHostModeParser.Parse(value);

        // Assert
        Assert.Equal(ComplianceHostMode.Standalone, mode);
    }

    [Theory]
    [InlineData("standalone", ComplianceHostMode.Standalone)]
    [InlineData("API", ComplianceHostMode.Api)]
    [InlineData("worker", ComplianceHostMode.Worker)]
    public void ShouldParseCaseInsensitivelyGivenSupportedMode(
        string value,
        ComplianceHostMode expected)
    {
        // Arrange
        var configuredValue = value;

        // Act
        var mode = ComplianceHostModeParser.Parse(configuredValue);

        // Assert
        Assert.Equal(expected, mode);
    }

    [Fact]
    public void ShouldFailClosedGivenUnsupportedMode()
    {
        // Arrange
        const string value = "unexpected";

        // Act
        var error = Assert.Throws<InvalidOperationException>(() =>
            ComplianceHostModeParser.Parse(value));

        // Assert
        Assert.Contains("COMPLIANCE_HOST_MODE", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ComplianceHostMode.Standalone, true, true)]
    [InlineData(ComplianceHostMode.Api, true, false)]
    [InlineData(ComplianceHostMode.Worker, false, true)]
    public void ShouldSelectResponsibilitiesGivenHostMode(
        ComplianceHostMode mode,
        bool runsApi,
        bool runsWorkers)
    {
        // Arrange
        var selectedMode = mode;

        // Act
        var actualRunsApi = selectedMode.RunsApi();
        var actualRunsWorkers = selectedMode.RunsWorkers();

        // Assert
        Assert.Equal(runsApi, actualRunsApi);
        Assert.Equal(runsWorkers, actualRunsWorkers);
    }
}
