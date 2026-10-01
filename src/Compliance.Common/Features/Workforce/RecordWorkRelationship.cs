using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     Records a person's work relationship under a stable source worker ID. The worker ID is
///     unique per tenant: recording it again for another person conflicts.
/// </summary>
[Discriminator("bdgrz.workforce.work-relationship.record", 1)]
public sealed record RecordWorkRelationship(Uuid TenantId, Uuid PersonId, string SourceWorkerId,
    string WorkerType, string LifecycleStatus, DateOnly StartDate, DateOnly? EndDate = null,
    string? Department = null, Uuid? ManagerPersonId = null, Uuid? SponsorPersonId = null,
    string? EmploymentStatusReason = null)
    : IRequest<WorkRelationshipRegistration>, IWorkforceRequest, ICallable;
