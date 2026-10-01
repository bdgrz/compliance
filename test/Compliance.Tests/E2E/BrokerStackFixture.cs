using Bdgrz.Compliance.Features.Programs;

namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     One broker stack shared by every test in
///     <see cref="BrokerCollectionDefinition" />, which also keeps the broker tests sequential.
/// </summary>
public sealed class BrokerStackFixture : IAsyncLifetime, IAsyncDisposable
{
    readonly BrokerStack _stack = new();

    public string WebSocketEndpoint => _stack.WebSocketEndpoint;

    public Task InitializeAsync() => _stack.StartAsync();

    public Task DisposeAsync() => _stack.DisposeAsync().AsTask();

    ValueTask IAsyncDisposable.DisposeAsync() => _stack.DisposeAsync();
}
