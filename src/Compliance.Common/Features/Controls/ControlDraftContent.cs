namespace Bdgrz.Compliance.Features.Controls;

public sealed record ControlDraftContent(string Title, string Objective, string Description,
    string ImplementationNarrative, IReadOnlyList<string> ExpectedEvidenceDescriptions,
    string? OwnerReference = null,
    IReadOnlyList<ControlApplicabilityReference>? Applicability = null);
