using Cntryl.Portia;
using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     Prevents control review and approval events from being emitted until every active reader
///     understands their discriminators.
/// </summary>
public sealed record ControlActivationReleaseGate(bool IsEnabled)
{
    public static ControlActivationReleaseGate FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new ControlActivationReleaseGate(bool.TryParse(
            configuration["Compliance:Controls:ActivationEnabled"], out var enabled) && enabled);
    }

    internal static RequestError Unavailable => new(RequestErrorKind.Conflict,
        "Control review and activation are unavailable until all active event readers are upgraded.");
}
