using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bdgrz.Compliance.Tests;

public sealed class ComplianceBrokerIntegrationTests
{
    [Fact]
    [Trait("Category", "BrokerIntegration")]
    public async Task ShouldStartStandaloneHostGivenRealFitzBroker()
    {
        // Arrange
        var endpoint = Environment.GetEnvironmentVariable("FITZ_TEST_ENDPOINT") ??
            "ws://127.0.0.1:4090/ws";
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Compliance:Authentication:Mode", "Development");
            builder.UseSetting("Fitz:Endpoint", endpoint);
            builder.UseSetting("Fitz:ApplicationName", $"compliance-tests-{Guid.NewGuid():N}");
            builder.UseSetting("Fitz:StartupTimeoutSeconds", "30");
        });
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/health/ready", CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
