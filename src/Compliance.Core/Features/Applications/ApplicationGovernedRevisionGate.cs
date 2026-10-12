using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Requires complete resulting content for application revisions when explicitly enabled.</summary>
public sealed record ApplicationGovernedRevisionGate(bool IsEnabled)
{
    public static ApplicationGovernedRevisionGate FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new ApplicationGovernedRevisionGate(bool.TryParse(
            configuration["Compliance:Applications:GovernedRevisionsEnabled"], out var enabled) && enabled);
    }
}
