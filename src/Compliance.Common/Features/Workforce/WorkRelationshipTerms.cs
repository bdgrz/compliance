using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     The M0-D06 minimum worker attributes other than identity. <c>ManagerPersonId</c> is a
///     restricted workforce field: single-record reads return it, lists redact it.
///     <c>SponsorPersonId</c> is the accountable internal sponsor an external collaborator needs.
/// </summary>
public sealed record WorkRelationshipTerms(string WorkerType, string LifecycleStatus,
    DateOnly StartDate, DateOnly? EndDate, string? Department, Uuid? ManagerPersonId,
    Uuid? SponsorPersonId);
