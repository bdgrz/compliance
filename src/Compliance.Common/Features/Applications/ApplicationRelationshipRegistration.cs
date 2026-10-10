using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed record ApplicationRelationshipRegistration(Uuid RelationshipId,
    long Revision, string Status);
