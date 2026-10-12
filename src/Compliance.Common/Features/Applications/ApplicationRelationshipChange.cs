using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed record ApplicationRelationshipChange(Uuid RelationshipId,
    long RelationshipRevision, string State);
