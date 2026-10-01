namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     Every broker test shares one broker stack per test process. Tests isolate themselves with
///     unique tenants and application names; only tests that restart or restore the broker own a
///     separate stack (<see cref="RestartableBrokerStackFixture" />).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BrokerCollectionDefinition : ICollectionFixture<BrokerStackFixture>
{
    public const string Name = "Broker e2e";
}
