namespace Bdgrz.Compliance.Tests.E2E;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ApplicationInventoryBrokerCollectionDefinition
    : ICollectionFixture<BrokerStackFixture>
{
    public const string Name = "Application inventory broker e2e";
}
