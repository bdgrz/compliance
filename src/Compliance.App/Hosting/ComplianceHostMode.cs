namespace Bdgrz.Compliance.Hosting;

/// <summary>Selects which application responsibilities run in the current process.</summary>
public enum ComplianceHostMode
{
    Standalone,
    Api,
    Worker,
}
