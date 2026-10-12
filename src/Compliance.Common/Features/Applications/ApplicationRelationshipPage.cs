using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed record ApplicationRelationshipPage(long RelationshipRevision,
    IReadOnlyList<ApplicationRelationshipView> Items, string? NextCursor);
