using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Requires complete fresh declarations after compatible readers and writers are deployed.</summary>
public sealed record ApplicationGovernedDeclarationGate(bool IsEnabled)
{
    public static ApplicationGovernedDeclarationGate FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new ApplicationGovernedDeclarationGate(bool.TryParse(
            configuration["Compliance:Applications:GovernedDeclarationsEnabled"], out var enabled) && enabled);
    }
}
