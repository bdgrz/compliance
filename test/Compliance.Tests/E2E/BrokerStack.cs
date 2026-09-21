using System.Diagnostics;
using System.Net.Http.Json;

namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     Owns the lifecycle of the e2e-only Fitz broker + storage stack (<c>E2E/compose.yml</c>):
///     starts it before the first e2e test runs, waits for the broker to actually report ready
///     (not just "container started"), and tears it down -- volumes included, so no state leaks
///     between runs -- after the last one finishes. Deliberately separate from the root
///     <c>compose.yml</c>, which is for running the whole app locally, not for tests.
/// </summary>
sealed class BrokerStack : IAsyncDisposable
{
    readonly string _projectName = $"bdgrz-compliance-e2e-{Guid.NewGuid():N}";
    int _httpPort;

    // The app connects over the broker's mapped HTTP port, which upgrades /ws.
    public string WebSocketEndpoint => _httpPort > 0
        ? $"ws://127.0.0.1:{_httpPort}/ws"
        : throw new InvalidOperationException("The e2e broker has not started.");

    bool _started;

    public async Task StartAsync(CancellationToken ct = default)
    {
        // Compose can create containers before `up` exits. Claim cleanup ownership before the
        // command so a failed or canceled startup still tears down this fixture's project.
        _started = true;
        try
        {
            await RunComposeAsync(ct, "up", "--detach").ConfigureAwait(false);
            var portBinding = (await RunComposeAsync(ct, "port", "broker", "4090")
                .ConfigureAwait(false)).Trim();
            var separator = portBinding.LastIndexOf(':');
            if (separator < 0 || !int.TryParse(portBinding[(separator + 1)..], out _httpPort) ||
                _httpPort is < 1 or > 65535)
                throw new InvalidOperationException($"Unexpected e2e broker port binding: {portBinding}");
            await WaitUntilReadyAsync(ct).ConfigureAwait(false);
        }
        catch (Exception startupError)
        {
            try
            {
                await DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("The e2e broker failed to start and clean up.",
                    startupError, cleanupError);
            }
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_started)
            return;

        await RunComposeAsync(CancellationToken.None, "down", "--volumes").ConfigureAwait(false);
        _started = false;
        _httpPort = 0;
    }

    async Task WaitUntilReadyAsync(CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        while (DateTimeOffset.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var health = await http.GetFromJsonAsync<BrokerHealth>(
                    $"http://127.0.0.1:{_httpPort}/health", ct).ConfigureAwait(false);
                if (health?.Status == "ready")
                    return;
            }
            catch (HttpRequestException)
            {
                // Broker isn't accepting connections yet -- keep polling.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), ct).ConfigureAwait(false);
        }

        throw new TimeoutException("The e2e Fitz broker did not report ready within 60 seconds.");
    }

    async Task<string> RunComposeAsync(CancellationToken ct, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            ArgumentList =
            {
                "compose", "-p", _projectName, "-f", ComposeFilePath(),
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the docker compose process.");
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"docker compose {string.Join(' ', arguments)} failed (exit {process.ExitCode}):\n" +
                $"{await stdout.ConfigureAwait(false)}\n{await stderr.ConfigureAwait(false)}");
        }

        return await stdout.ConfigureAwait(false);
    }

    static string ComposeFilePath() =>
        Path.Combine(AppContext.BaseDirectory, "E2E", "compose.yml");

    sealed record BrokerHealth(string Status);
}
