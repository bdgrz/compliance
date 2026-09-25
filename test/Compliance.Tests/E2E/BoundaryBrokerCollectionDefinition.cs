namespace Bdgrz.Compliance.Tests.E2E;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BoundaryBrokerCollectionDefinition : ICollectionFixture<BrokerStackFixture>
{
    public const string Name = "Boundary broker e2e";
}
