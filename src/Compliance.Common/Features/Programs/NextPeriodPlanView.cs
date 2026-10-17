using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>Proposed successor-period dates and carry-forward plan.</summary>
public sealed record NextPeriodPlanView(Uuid TenantId, Uuid ProgramId, Uuid NextPeriodPlanId, string Status, long Revision, DateTimeOffset UpdatedAt);
