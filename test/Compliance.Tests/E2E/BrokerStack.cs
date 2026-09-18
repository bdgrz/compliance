using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;

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
    const string ProjectName = "bdgrz-compliance-e2e";
    const int HttpPort = 14090;

    // The app connects over the broker's HTTP port, which upgrades /ws -- same as the root
    // compose.yml's Fitz__Endpoint. compose.yml also maps a separate raw-TCP port the app doesn't use.
    public string WebSocketEndpoint { get; } = $"ws://127.0.0.1:{HttpPort}/ws";

    bool _started;

    public async Task StartAsync(CancellationToken ct = default)
    {
        await RunComposeAsync("up --detach", ct).ConfigureAwait(false);
        _started = true;
        await WaitUntilReadyAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_started)
            return;

        await RunComposeAsync("down --volumes", CancellationToken.None).ConfigureAwait(false);
    }

    static async Task WaitUntilReadyAsync(CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        while (DateTimeOffset.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var health = await http.GetFromJsonAsync<BrokerHealth>(
                    $"http://127.0.0.1:{HttpPort}/health", ct).ConfigureAwait(false);
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

    static async Task RunComposeAsync(string arguments, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            ArgumentList =
            {
                "compose", "-p", ProjectName, "-f", ComposeFilePath(),
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments.Split(' '))
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the docker compose process.");
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"docker compose {arguments} failed (exit {process.ExitCode}):\n" +
                $"{await stdout.ConfigureAwait(false)}\n{await stderr.ConfigureAwait(false)}");
        }
    }

    static string ComposeFilePath([CallerFilePath] string sourceFile = "") =>
        Path.Combine(Path.GetDirectoryName(sourceFile)!, "compose.yml");

    sealed record BrokerHealth(string Status);
}
