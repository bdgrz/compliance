using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

/// <summary>Lets other contexts verify a commitment reference against the authoritative stream.</summary>
public interface ICommitmentReferenceReader
{
    ValueTask<bool> IsRecordedAsync(Uuid tenantId, Uuid draftId, CancellationToken ct = default);
}
