using Cntryl.Portia;
using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     Prevents successor, retirement-proposal, and retirement events from being emitted until
///     every active reader understands their discriminators.
/// </summary>
public sealed record ControlLifecycleReleaseGate(bool IsEnabled)
{
    public static ControlLifecycleReleaseGate FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new ControlLifecycleReleaseGate(bool.TryParse(
            configuration["Compliance:Controls:LifecycleEnabled"], out var enabled) && enabled);
    }

    internal static RequestError Unavailable => new(RequestErrorKind.Conflict,
        "Control successors and retirement are unavailable until all active event readers are upgraded.");
}
