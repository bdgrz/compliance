using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Engagement identity, period, lifecycle, and baseline binding.</summary>
public sealed record ExaminationEngagementView(Uuid TenantId, Uuid ProgramId, Uuid ExaminationEngagementId, string Status, long Revision, DateTimeOffset UpdatedAt);
