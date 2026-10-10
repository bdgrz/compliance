using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class UnconfiguredServiceEngagementAcceptanceEvidenceReader : IServiceEngagementAcceptanceEvidenceReader
{
    public ValueTask<Result<ServiceEngagementAcceptanceEvidence>> ReadCurrentAsync(Uuid tenantId,
        Uuid engagementId, Uuid actorUserId, CancellationToken ct) => ValueTask.FromResult(
        Result<ServiceEngagementAcceptanceEvidence>.Failure(new RequestError(RequestErrorKind.Conflict,
            "Current engagement authority and ratified rules are unavailable; acceptance was not recorded.", true)));
}
