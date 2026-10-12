using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed record ApplicationRelationshipView(Uuid RelationshipId,
    Uuid TargetApplicationId, string RelationshipType, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset ChangedAt);
