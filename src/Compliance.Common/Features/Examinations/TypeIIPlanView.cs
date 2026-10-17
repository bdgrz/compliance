using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Proposed or approved period plan and decision history.</summary>
public sealed record TypeIIPlanView(Uuid TenantId, Uuid ProgramId, Uuid TypeIIPlanId, string Status, long Revision, DateTimeOffset UpdatedAt);
