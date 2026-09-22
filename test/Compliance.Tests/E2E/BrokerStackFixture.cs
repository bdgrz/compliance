using System.Reflection;
using Bdgrz.Compliance.Features.Programs;

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

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BrokerCollectionDefinition
{
    public const string Name = "Broker e2e";
}
