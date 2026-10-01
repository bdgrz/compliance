namespace Bdgrz.Compliance.Features.Operations;

/// <summary>One expected period of a recurring cadence and the date its performance is due.</summary>
public sealed record ExpectedPeriod(DateOnly Start, DateOnly End, DateOnly DueOn);
