using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

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
    string _projectName = CreateProjectName();
    readonly string _composeFileName;
    int _httpPort;

    // The app connects over the broker's mapped HTTP port, which upgrades /ws.
    public string WebSocketEndpoint => _httpPort > 0
        ? $"ws://127.0.0.1:{_httpPort}/ws"
        : throw new InvalidOperationException("The e2e broker has not started.");

    bool _started;

    public BrokerStack(string composeFileName = "compose.yml")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(composeFileName);
        _composeFileName = composeFileName;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        // Compose can create containers before `up` exits. Claim cleanup ownership before the
        // command so a failed or canceled startup still tears down this fixture's project.
        _started = true;
        try
        {
            await RunComposeAsync(ct, "up", "--detach").ConfigureAwait(false);
            await RefreshPortAsync(ct).ConfigureAwait(false);
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

    /// <summary>
    ///     Stops and recreates only the Fitz container while retaining Compose-managed data volumes.
    ///     This gives recovery probes a fresh broker process against the same durable local store.
    /// </summary>
    public async Task RestartAsync(CancellationToken ct = default)
    {
        if (!_started)
            throw new InvalidOperationException("The e2e broker has not started.");

        try
        {
            await RunComposeAsync(ct, "stop", "broker").ConfigureAwait(false);
            await RunComposeAsync(ct, "rm", "--force", "broker").ConfigureAwait(false);
            _httpPort = 0;
            await RunComposeAsync(ct, "up", "--detach", "broker").ConfigureAwait(false);
            await RefreshPortAsync(ct).ConfigureAwait(false);
            await WaitUntilReadyAsync(ct).ConfigureAwait(false);
        }
        catch (Exception restartError)
        {
            try
            {
                await DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("The e2e broker failed to restart and clean up.",
                    restartError, cleanupError);
            }

            throw;
        }
    }

    /// <summary>
    ///     Exports the stopped broker's local durable store, destroys its Compose project and
    ///     volume, then imports that archive into a never-started broker in a fresh project.
    /// </summary>
    public async Task<PortableBrokerBackupRestore> BackupAndRestoreAsync(
        string eventReaderVersion, CancellationToken ct = default)
    {
        if (!_started)
            throw new InvalidOperationException("The e2e broker has not started.");
        ArgumentException.ThrowIfNullOrWhiteSpace(eventReaderVersion);

        string? archivePath = null;
        string? manifestPath = null;
        try
        {
            await RunComposeAsync(ct, "stop", "broker").ConfigureAwait(false);
            var sourceProject = _projectName;
            var sourceContainer = await GetBrokerContainerIdAsync(ct).ConfigureAwait(false);
            var sourceVolume = await GetBrokerDataVolumeAsync(sourceContainer, ct)
                .ConfigureAwait(false);
            var brokerImage = await GetBrokerImageAsync(sourceContainer, ct).ConfigureAwait(false);
            archivePath = Path.Combine(Path.GetTempPath(),
                $"bdgrz-compliance-fitz-{Guid.NewGuid():N}.tar");
            await ExportContainerDirectoryAsync(sourceContainer, "/data/.", archivePath, ct)
                .ConfigureAwait(false);
            var archiveSha256 = await CalculateSha256Async(archivePath, ct).ConfigureAwait(false);
            manifestPath = $"{archivePath}.manifest.json";
            await WriteArchiveManifestAsync(manifestPath, new PortableBrokerArchiveManifest(
                sourceProject, sourceVolume, brokerImage, eventReaderVersion, archiveSha256),
                ct).ConfigureAwait(false);

            await RunComposeAsync(ct, "down", "--volumes").ConfigureAwait(false);
            await EnsureVolumeWasRemovedAsync(sourceVolume, ct).ConfigureAwait(false);
            _started = false;
            _httpPort = 0;

            _projectName = CreateProjectName();
            _started = true;
            await RunComposeAsync(ct, "create", "broker").ConfigureAwait(false);
            var restoredContainer = await GetBrokerContainerIdAsync(ct).ConfigureAwait(false);
            var restoredVolume = await GetBrokerDataVolumeAsync(restoredContainer, ct)
                .ConfigureAwait(false);
            if (string.Equals(sourceVolume, restoredVolume, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The recovery broker reused the source durable-data volume.");
            }

            var archiveManifest = await ReadArchiveManifestAsync(manifestPath!, ct)
                .ConfigureAwait(false);
            await ValidateArchiveManifestAsync(archiveManifest, new PortableBrokerArchiveManifest(
                sourceProject, sourceVolume, brokerImage, eventReaderVersion, archiveSha256),
                archivePath, ct).ConfigureAwait(false);
            var restoredBrokerImage = await GetBrokerImageAsync(restoredContainer, ct)
                .ConfigureAwait(false);
            if (!string.Equals(archiveManifest.BrokerImage, restoredBrokerImage,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The recovery broker image does not match the archived source image.");
            }

            await ImportContainerDirectoryAsync(archivePath, restoredContainer, "/data", ct)
                .ConfigureAwait(false);
            await RunComposeAsync(ct, "start", "broker").ConfigureAwait(false);
            await RefreshPortAsync(ct).ConfigureAwait(false);
            await WaitUntilReadyAsync(ct).ConfigureAwait(false);

            return new PortableBrokerBackupRestore(sourceProject, _projectName, sourceVolume,
                restoredVolume, brokerImage, restoredBrokerImage, eventReaderVersion, archiveSha256);
        }
        catch (Exception recoveryError)
        {
            try
            {
                await DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("The e2e broker failed to restore and clean up.",
                    recoveryError, cleanupError);
            }

            throw;
        }
        finally
        {
            if (manifestPath is not null)
                File.Delete(manifestPath);
            if (archivePath is not null)
                File.Delete(archivePath);
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

    async Task RefreshPortAsync(CancellationToken ct)
    {
        var portBinding = (await RunComposeAsync(ct, "port", "broker", "4090")
            .ConfigureAwait(false)).Trim();
        var separator = portBinding.LastIndexOf(':');
        if (separator < 0 || !int.TryParse(portBinding[(separator + 1)..], out _httpPort) ||
            _httpPort is < 1 or > 65535)
        {
            throw new InvalidOperationException($"Unexpected e2e broker port binding: {portBinding}");
        }
    }

    async Task<string> GetBrokerContainerIdAsync(CancellationToken ct)
    {
        var containerIds = (await RunComposeAsync(ct, "ps", "--all", "--quiet", "broker")
            .ConfigureAwait(false)).Split('\n', StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);
        return containerIds.Length == 1
            ? containerIds[0]
            : throw new InvalidOperationException(
                $"Expected one e2e broker container, found {containerIds.Length}.");
    }

    static async Task<string> GetBrokerDataVolumeAsync(string containerId, CancellationToken ct)
    {
        var volume = (await RunDockerAsync(ct, "container", "inspect", "--format",
            "{{range .Mounts}}{{if eq .Destination \"/data\"}}{{.Name}}{{end}}{{end}}", containerId)
            .ConfigureAwait(false)).Trim();
        return !string.IsNullOrWhiteSpace(volume) && !volume.Contains('\n')
            ? volume
            : throw new InvalidOperationException(
                "The e2e broker container did not have exactly one durable /data volume.");
    }

    static async Task<string> GetBrokerImageAsync(string containerId, CancellationToken ct)
    {
        var image = (await RunDockerAsync(ct, "container", "inspect", "--format", "{{.Config.Image}}",
            containerId).ConfigureAwait(false)).Trim();
        return !string.IsNullOrWhiteSpace(image)
            ? image
            : throw new InvalidOperationException("The e2e broker container did not report an image.");
    }

    static async Task EnsureVolumeWasRemovedAsync(string volumeName, CancellationToken ct)
    {
        var inspection = await RunDockerResultAsync(ct, "volume", "inspect", volumeName)
            .ConfigureAwait(false);
        if (inspection.ExitCode == 0)
        {
            throw new InvalidOperationException(
                $"The source durable-data volume {volumeName} still exists after cleanup.");
        }

        if (!inspection.StandardError.Contains("no such volume", StringComparison.OrdinalIgnoreCase) &&
            !inspection.StandardError.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Could not verify removal of source durable-data volume {volumeName} " +
                $"(exit {inspection.ExitCode}):\n{inspection.StandardOutput}\n" +
                inspection.StandardError);
        }
    }

    static async Task<string> CalculateSha256Async(string archivePath, CancellationToken ct)
    {
        await using var archive = new FileStream(archivePath, FileMode.Open, FileAccess.Read,
            FileShare.Read);
        return Convert.ToHexString(await SHA256.HashDataAsync(archive, ct).ConfigureAwait(false));
    }

    static async Task WriteArchiveManifestAsync(string manifestPath,
        PortableBrokerArchiveManifest manifest, CancellationToken ct)
    {
        await using var output = new FileStream(manifestPath, FileMode.CreateNew, FileAccess.Write,
            FileShare.None);
        await JsonSerializer.SerializeAsync(output, manifest, cancellationToken: ct)
            .ConfigureAwait(false);
    }

    static async Task<PortableBrokerArchiveManifest> ReadArchiveManifestAsync(string manifestPath,
        CancellationToken ct)
    {
        await using var input = new FileStream(manifestPath, FileMode.Open, FileAccess.Read,
            FileShare.Read);
        return await JsonSerializer.DeserializeAsync<PortableBrokerArchiveManifest>(input,
                   cancellationToken: ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The local broker archive has no manifest.");
    }

    static async Task ValidateArchiveManifestAsync(PortableBrokerArchiveManifest actual,
        PortableBrokerArchiveManifest expected, string archivePath, CancellationToken ct)
    {
        if (actual != expected)
            throw new InvalidOperationException("The local broker archive manifest is inconsistent.");

        var currentArchiveSha256 = await CalculateSha256Async(archivePath, ct).ConfigureAwait(false);
        if (!string.Equals(actual.ArchiveSha256, currentArchiveSha256, StringComparison.Ordinal))
            throw new InvalidOperationException("The local broker archive checksum does not match.");
    }

    static async Task ExportContainerDirectoryAsync(string containerId, string sourcePath, string archivePath,
        CancellationToken ct)
    {
        using var process = StartDockerProcess("container", "cp", "--archive",
            $"{containerId}:{sourcePath}", "-");
        var stderr = process.StandardError.ReadToEndAsync(ct);
        await using var archive = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write,
            FileShare.None);
        try
        {
            await process.StandardOutput.BaseStream.CopyToAsync(archive, ct).ConfigureAwait(false);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        var standardError = await stderr.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"docker container cp export failed (exit {process.ExitCode}):\n{standardError}");
        }
    }

    static async Task ImportContainerDirectoryAsync(string archivePath, string containerId,
        string destinationPath, CancellationToken ct)
    {
        using var process = StartDockerProcess("container", "cp", "--archive", "-",
            $"{containerId}:{destinationPath}");
        var stderr = process.StandardError.ReadToEndAsync(ct);
        await using var archive = new FileStream(archivePath, FileMode.Open, FileAccess.Read,
            FileShare.Read);
        try
        {
            await archive.CopyToAsync(process.StandardInput.BaseStream, ct).ConfigureAwait(false);
            process.StandardInput.Close();
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        var standardError = await stderr.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"docker container cp import failed (exit {process.ExitCode}):\n{standardError}");
        }
    }

    async Task<string> RunComposeAsync(CancellationToken ct, params string[] arguments)
    {
        var dockerArguments = new List<string>
        {
            "compose", "-p", _projectName, "-f", ComposeFilePath(),
        };
        dockerArguments.AddRange(arguments);
        return await RunDockerAsync(ct, dockerArguments.ToArray()).ConfigureAwait(false);
    }

    static async Task<string> RunDockerAsync(CancellationToken ct, params string[] arguments)
    {
        var result = await RunDockerResultAsync(ct, arguments).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"docker {string.Join(' ', arguments)} failed (exit {result.ExitCode}):\n" +
                $"{result.StandardOutput}\n{result.StandardError}");
        }

        return result.StandardOutput;
    }

    static async Task<DockerCommandResult> RunDockerResultAsync(CancellationToken ct,
        params string[] arguments)
    {
        using var process = StartDockerProcess(arguments);
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

        return new DockerCommandResult(process.ExitCode, await stdout.ConfigureAwait(false),
            await stderr.ConfigureAwait(false));
    }

    static Process StartDockerProcess(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the docker process.");
    }

    static string CreateProjectName() => $"bdgrz-compliance-e2e-{Guid.NewGuid():N}";

    string ComposeFilePath() =>
        Path.Combine(AppContext.BaseDirectory, "E2E", _composeFileName);

    sealed record PortableBrokerArchiveManifest(string SourceProject, string SourceVolume,
        string BrokerImage, string EventReaderVersion, string ArchiveSha256);

    sealed record DockerCommandResult(int ExitCode, string StandardOutput, string StandardError);

    sealed record BrokerHealth(string Status);
}
