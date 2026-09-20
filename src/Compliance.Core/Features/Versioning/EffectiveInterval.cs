namespace Bdgrz.Compliance.Features.Versioning;

/// <summary>A half-open effective interval for one immutable approved version.</summary>
public readonly record struct EffectiveInterval
{
    public DateOnly From { get; }
    public DateOnly? UntilExclusive { get; }

    public EffectiveInterval(DateOnly from, DateOnly? untilExclusive)
    {
        if (untilExclusive is { } until && !CanFollow(from, until))
            throw new ArgumentOutOfRangeException(nameof(untilExclusive),
                "An effective interval must end after it begins.");
        From = from;
        UntilExclusive = untilExclusive;
    }

    public bool Contains(DateOnly date) => date >= From &&
        (UntilExclusive is null || date < UntilExclusive);

    public static bool CanFollow(DateOnly previousFrom, DateOnly nextFrom) =>
        nextFrom > previousFrom;
}
