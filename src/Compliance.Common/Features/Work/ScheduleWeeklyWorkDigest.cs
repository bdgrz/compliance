using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Records the schedule weekly work digest intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.work.schedule-weekly-work-digest", 1)]
public sealed record ScheduleWeeklyWorkDigest(Uuid TenantId, DateOnly WeekStarting, string ScheduleKey) : IRequest, IClientManagementMutationRequest;
