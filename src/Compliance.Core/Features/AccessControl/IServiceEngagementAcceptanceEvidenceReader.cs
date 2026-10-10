using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Resolves current, authoritative acceptance evidence for one exact client draft. Implementations must fail
///     closed unless partner-duty authority, the current directory and source revisions, and applicable ratified
///     rules are all proven from trusted read sources.
/// </summary>
public interface IServiceEngagementAcceptanceEvidenceReader
{
    ValueTask<Result<ServiceEngagementAcceptanceEvidence>> ReadCurrentAsync(Uuid tenantId, Uuid engagementId,
        Uuid actorUserId, CancellationToken ct);
}
