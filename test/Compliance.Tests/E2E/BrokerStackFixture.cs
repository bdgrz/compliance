namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     Owns one broker stack per test class. Workers scan shared tenant/identity source patterns,
///     so unique tenant and application names alone do not isolate earlier classes' history.
///     <see cref="BrokerCollectionDefinition" /> runs the classes one at a time.
/// </summary>
public sealed class BrokerStackFixture : IAsyncLifetime, IAsyncDisposable
{
    BrokerStack? _stack;

    public string WebSocketEndpoint =>
        (_stack ?? throw new InvalidOperationException("The broker has not started.")).WebSocketEndpoint;

    public async Task InitializeAsync()
    {
        _stack = new BrokerStack();
        await _stack.StartAsync();
    }

    public Task DisposeAsync() => ((IAsyncDisposable)this).DisposeAsync().AsTask();

    ValueTask IAsyncDisposable.DisposeAsync() => _stack?.DisposeAsync() ?? ValueTask.CompletedTask;
}
