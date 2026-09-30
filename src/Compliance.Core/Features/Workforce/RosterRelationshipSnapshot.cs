using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>The last accepted roster version of a relationship, compared to observe changes.</summary>
public sealed record RosterRelationshipSnapshot(Uuid TenantId, Uuid RelationshipId, Uuid PersonId,
    string SourceWorkerId, long Revision, WorkRelationshipTerms Terms);
