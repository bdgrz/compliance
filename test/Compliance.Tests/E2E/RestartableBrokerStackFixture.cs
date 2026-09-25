using System.Reflection;

namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     Owns the local-disk recovery fixture. Restarting recreates the broker container without
///     removing its project-scoped volume so a test can distinguish process recovery from a warm
///     in-memory broker.
/// </summary>
public sealed class RestartableBrokerStackFixture : IAsyncLifetime, IAsyncDisposable
{
    readonly BrokerStack _stack = new("recovery.compose.yml");

    public string WebSocketEndpoint => _stack.WebSocketEndpoint;

    public Task InitializeAsync() => _stack.StartAsync();

    public Task RestartAsync() => _stack.RestartAsync();

    public Task<PortableBrokerBackupRestore> BackupAndRestoreAsync() =>
        _stack.BackupAndRestoreAsync(typeof(ProgramDirectoryProjector).Assembly
                                         .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                                     ?? throw new InvalidOperationException("The program event-reader has no version."));

    public Task DisposeAsync() => _stack.DisposeAsync().AsTask();

    ValueTask IAsyncDisposable.DisposeAsync() => _stack.DisposeAsync();
}
