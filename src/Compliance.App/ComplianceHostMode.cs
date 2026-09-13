namespace Bdgrz.Compliance;

/// <summary>
/// Selects which application responsibilities run in the current process.
/// </summary>
public enum ComplianceHostMode
{
    /// <summary>
    /// Hosts the HTTP API and activates background workers.
    /// </summary>
    Standalone,

    /// <summary>
    /// Hosts the HTTP API without activating background workers.
    /// </summary>
    Api,

    /// <summary>
    /// Activates background workers without hosting the HTTP API.
    /// </summary>
    Worker,
}

/// <summary>
/// Parses and describes the process-level compliance host mode.
/// </summary>
public static class ComplianceHostModeParser
{
    /// <summary>
    /// Parses <c>COMPLIANCE_HOST_MODE</c>, defaulting to standalone hosting.
    /// </summary>
    public static ComplianceHostMode Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ComplianceHostMode.Standalone;
        }

        if (Enum.TryParse<ComplianceHostMode>(value.Trim(), ignoreCase: true, out var mode) &&
            Enum.IsDefined(mode))
        {
            return mode;
        }

        throw new InvalidOperationException(
            $"COMPLIANCE_HOST_MODE must be one of standalone, api, or worker; received '{value}'.");
    }

    /// <summary>
    /// Returns whether the selected mode hosts HTTP endpoints.
    /// </summary>
    public static bool RunsApi(this ComplianceHostMode mode) =>
        mode is ComplianceHostMode.Standalone or ComplianceHostMode.Api;

    /// <summary>
    /// Returns whether the selected mode activates background workers.
    /// </summary>
    public static bool RunsWorkers(this ComplianceHostMode mode) =>
        mode is ComplianceHostMode.Standalone or ComplianceHostMode.Worker;
}
