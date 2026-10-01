namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     Broker tests run one class at a time: every test host polls the shared broker, and running
///     them concurrently saturates it.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BrokerCollectionDefinition
{
    public const string Name = "Broker e2e";
}
