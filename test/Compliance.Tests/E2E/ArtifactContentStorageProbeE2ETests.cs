using System.Net;
using Cntryl.Fitz;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ArtifactContentStorageProbeE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldRejectTwoHundredFiftySixKiBContentGivenFitzWireLimit()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var http = factory.CreateClient();
        using var ready = await http.GetAsync("/health/ready", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        var store = factory.Services.GetRequiredService<IKvClient>();
        var tenantId = Uuid.CreateVersion4();
        await using var transaction = await store.BeginAsync(
            $"kv://{tenantId}/artifact_content/v1", KvDurability.Sync, KvMode.ReadWrite);
        var content = GC.AllocateUninitializedArray<byte>(256 * 1024);

        // Act
        var exception = await Assert.ThrowsAsync<ProtocolException>(() => transaction.InsertAsync(
            "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"u8.ToArray(),
            content));

        // Assert
        Assert.Contains("payload", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
