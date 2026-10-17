using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Auditor grants and revocations scoped to an engagement.</summary>
public sealed record EngagementAccessView(Uuid TenantId, Uuid ProgramId, Uuid EngagementAccessId, string Status, long Revision, DateTimeOffset UpdatedAt);
