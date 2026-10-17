using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Current or requested version, review/approval state, and engagement binding.</summary>
public sealed record SystemDescriptionView(Uuid TenantId, Uuid ProgramId, Uuid SystemDescriptionId, string Status, long Revision, DateTimeOffset UpdatedAt);
