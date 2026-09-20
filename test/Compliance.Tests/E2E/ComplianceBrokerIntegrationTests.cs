using System.Net;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ComplianceBrokerIntegrationTests(BrokerStackFixture broker) : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldStartStandaloneHostGivenRealFitzBroker()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/health/ready", CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
