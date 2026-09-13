using Bdgrz.Compliance;

namespace Bdgrz.Compliance.Tests;

public sealed class AssemblyLayoutTests
{
    [Fact]
    public void ShouldExposeDistinctAssemblyNamesGivenLayeredProjects()
    {
        // Arrange
        var expectedNames = new[]
        {
            "Bdgrz.Compliance.Common",
            "Bdgrz.Compliance.Core",
            "Bdgrz.Compliance.App",
        };

        // Act
        var actualNames = new[]
        {
            typeof(CommonAssembly).Assembly.GetName().Name,
            typeof(CoreAssembly).Assembly.GetName().Name,
            typeof(Program).Assembly.GetName().Name,
        };

        // Assert
        Assert.Equal(expectedNames, actualNames);
    }
}
