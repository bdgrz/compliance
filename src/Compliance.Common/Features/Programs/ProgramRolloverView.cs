using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>Rollover proposal, decision, and successor-period binding.</summary>
public sealed record ProgramRolloverView(Uuid TenantId, Uuid ProgramId, Uuid ProgramRolloverId, string Status, long Revision, DateTimeOffset UpdatedAt);
