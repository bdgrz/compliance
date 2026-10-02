namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     A provider-management statement about the interval after a report period. It is not an
///     auditor opinion and never closes a coverage gap.
/// </summary>
public sealed record AssuranceBridgeLetter(DateOnly LetterDate, DateOnly CoversFrom, DateOnly CoversThrough);
