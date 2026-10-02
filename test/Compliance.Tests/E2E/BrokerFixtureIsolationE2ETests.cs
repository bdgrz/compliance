namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class BrokerFixtureIsolationE2ETests
{
    [Fact]
    public async Task ShouldUseIndependentBrokersGivenSeparateClassFixtures()
    {
        // Arrange
        var first = new BrokerStackFixture();
        var second = new BrokerStackFixture();
        try
        {
            await first.InitializeAsync();
            await second.InitializeAsync();

            // Act
            var firstEndpoint = first.WebSocketEndpoint;
            var secondEndpoint = second.WebSocketEndpoint;

            // Assert
            Assert.NotEqual(firstEndpoint, secondEndpoint);
        }
        finally
        {
            await second.DisposeAsync();
            await first.DisposeAsync();
        }
    }
}
