using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Replaces a draft population's observed facts; an accepted population cannot change.</summary>
[Discriminator("bdgrz.access_population.facts.record", 1)]
public sealed record RecordAccessPopulationFacts(Uuid TenantId, Uuid PopulationId,
    long ExpectedRevision, IReadOnlyList<AccessPrincipalFact> Principals,
    IReadOnlyList<AccessEntitlementFact> Entitlements,
    IReadOnlyList<AccessGroupMemberFact> GroupMembers,
    IReadOnlyList<AccessAssignmentFact> Assignments)
    : IRequest<AccessPopulationRegistration>, IAccessReviewMutationRequest, ICallable;
