using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>One scoped advisor handoff, including selected immutable record versions and lifecycle state.</summary>
public sealed record AdvisorRequestView(Uuid TenantId, Uuid ProgramId, Uuid AdvisorRequestId, string Status, long Revision, DateTimeOffset UpdatedAt);
