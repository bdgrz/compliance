using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Request, assignment, due state, responses, and disclosure state.</summary>
public sealed record AuditorRequestView(Uuid TenantId, Uuid ProgramId, Uuid AuditorRequestId, string Status, long Revision, DateTimeOffset UpdatedAt);
