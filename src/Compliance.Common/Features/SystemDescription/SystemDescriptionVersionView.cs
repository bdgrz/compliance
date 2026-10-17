using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Version history with authorship and decision state.</summary>
public sealed record SystemDescriptionVersionView(Uuid TenantId, Uuid ProgramId, Uuid SystemDescriptionVersionId, string Status, long Revision, DateTimeOffset UpdatedAt);
