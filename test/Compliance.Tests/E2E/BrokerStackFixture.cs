namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     The one broker stack every broker test class shares. It starts on first use and stops when
///     the test process exits; tests isolate themselves with unique tenants and application names,
///     so classes run in parallel against it.
/// </summary>
public sealed class BrokerStackFixture : IAsyncLifetime
{
    static readonly Lazy<Task<BrokerStack>> Shared = new(StartSharedAsync);

    BrokerStack? _stack;

    public string WebSocketEndpoint =>
        (_stack ?? throw new InvalidOperationException("The broker has not started.")).WebSocketEndpoint;

    public async Task InitializeAsync() => _stack = await Shared.Value;

    public Task DisposeAsync() => Task.CompletedTask;

    static async Task<BrokerStack> StartSharedAsync()
    {
        var stack = new BrokerStack();
        await stack.StartAsync();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => stack.DisposeAsync().AsTask().GetAwaiter().GetResult();
        return stack;
    }
}
