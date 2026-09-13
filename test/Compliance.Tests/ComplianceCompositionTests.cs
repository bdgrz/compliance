using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests;

public sealed class ComplianceCompositionTests
{
    [Fact]
    public void ShouldReturnPortiaBuilderGivenSharedApplicationComposition()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            })
            .Build();

        // Act
        var application = services.AddCompliance(configuration);

        // Assert
        Assert.NotNull(application);
    }
}
