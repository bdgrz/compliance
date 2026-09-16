namespace Bdgrz.Compliance.Hosting;

/// <summary>Parses and describes the process-level compliance host mode.</summary>
public static class ComplianceHostModeParser
{
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

    public static bool RunsApi(this ComplianceHostMode mode) =>
        mode is ComplianceHostMode.Standalone or ComplianceHostMode.Api;

    public static bool RunsWorkers(this ComplianceHostMode mode) =>
        mode is ComplianceHostMode.Standalone or ComplianceHostMode.Worker;
}
