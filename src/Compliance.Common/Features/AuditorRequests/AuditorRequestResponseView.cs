using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Submitted responses and governed delivery/disposition receipts.</summary>
public sealed record AuditorRequestResponseView(Uuid TenantId, Uuid ProgramId, Uuid AuditorRequestResponseId, Uuid AuditorRequestId, string Status, DateTimeOffset SubmittedAt, Uuid? ArtifactId);
