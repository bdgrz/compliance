namespace Bdgrz.Compliance.Features.Policies;

/// <summary>The projected policy row plus the draft facts needed to apply its next decision.</summary>
public sealed record PolicyDirectoryRow(PolicySummaryView Summary, string DraftTitle,
    int DraftReviewCadenceMonths, int? CurrentReviewCadenceMonths, DateOnly? LastReviewedOn,
    bool RetirementPending);
