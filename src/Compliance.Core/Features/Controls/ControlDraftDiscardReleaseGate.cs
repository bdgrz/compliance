using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     Prevents a new discard event from being emitted until every active reader
///     understands its discriminator.
/// </summary>
public sealed record ControlDraftDiscardReleaseGate(bool IsEnabled)
{
    public static ControlDraftDiscardReleaseGate FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new ControlDraftDiscardReleaseGate(bool.TryParse(
            configuration["Compliance:Controls:DiscardEnabled"], out var enabled) && enabled);
    }
}
