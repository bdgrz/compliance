namespace Bdgrz.Compliance.Features.Responsibilities;

public sealed record ResponsibilityConflictPreview(ResponsibilityScope Scope, long Revision,
    IReadOnlyList<ResponsibilityConflictView> Conflicts);
