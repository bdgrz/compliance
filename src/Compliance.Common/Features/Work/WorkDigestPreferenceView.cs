namespace Bdgrz.Compliance.Features.Work;

/// <summary>Whether the member receives the weekly email digest; enabled unless they opted out.</summary>
public sealed record WorkDigestPreferenceView(bool EmailDigestEnabled, DateTimeOffset? ChangedAt)
{
    /// <summary>The member's validated time zone used for weekly email scheduling.</summary>
    public string TimeZoneId { get; init; } = "UTC";
}
