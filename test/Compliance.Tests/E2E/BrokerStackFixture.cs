namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     Shared across every test in <see cref="BrokerCollectionDefinition" /> so the e2e broker
///     stack starts once per test run, not once per test class -- docker compose startup is the
///     expensive part.
/// </summary>
public sealed class BrokerStackFixture : IAsyncLifetime, IAsyncDisposable
{
    readonly BrokerStack _stack = new();

    public string WebSocketEndpoint => _stack.WebSocketEndpoint;

    public Task InitializeAsync() => _stack.StartAsync();

    public Task DisposeAsync() => _stack.DisposeAsync().AsTask();

    ValueTask IAsyncDisposable.DisposeAsync() => _stack.DisposeAsync();
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BrokerCollectionDefinition : ICollectionFixture<BrokerStackFixture>
{
    public const string Name = "Broker e2e";
}
