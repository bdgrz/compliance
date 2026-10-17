using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Management-authored assertion and signature/status.</summary>
public sealed record ManagementAssertionView(Uuid TenantId, Uuid ProgramId, Uuid ManagementAssertionId, string Status, long Revision, DateTimeOffset UpdatedAt);
