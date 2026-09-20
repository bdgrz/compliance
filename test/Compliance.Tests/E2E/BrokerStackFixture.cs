namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     Shared across tests in one e2e class. Each class gets a fresh broker history while
///     <see cref="BrokerCollectionDefinition" /> keeps the broker tests sequential.
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
public sealed class BrokerCollectionDefinition
{
    public const string Name = "Broker e2e";
}
