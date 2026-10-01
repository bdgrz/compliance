using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Replaces a work relationship's terms; the person and source worker ID never change.</summary>
[Discriminator("bdgrz.workforce.work-relationship.revise", 1)]
public sealed record ReviseWorkRelationship(Uuid TenantId, Uuid RelationshipId,
    long ExpectedRevision, string WorkerType, string LifecycleStatus, DateOnly StartDate,
    DateOnly? EndDate = null, string? Department = null, Uuid? ManagerPersonId = null,
    Uuid? SponsorPersonId = null, string? EmploymentStatusReason = null)
    : IRequest, IWorkforceRequest, ICallable;
