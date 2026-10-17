using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Sign-off record, author, timestamp, and exact package/period references.</summary>
public sealed record ExaminationSignOffView(Uuid TenantId, Uuid ProgramId, Uuid ExaminationSignOffId, string Status, long Revision, DateTimeOffset UpdatedAt);
