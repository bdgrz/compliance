namespace Bdgrz.Compliance.Features.Risks;

public sealed record RiskDraftContent(string Title, string Scenario,
    string PotentialEffect, string? SourceNote);
