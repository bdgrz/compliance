using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

sealed class EventSourcedCommitmentReferenceReader(IAggregateReader reader)
    : ICommitmentReferenceReader
{
    public async ValueTask<bool> IsRecordedAsync(Uuid tenantId, Uuid draftId,
        CancellationToken ct = default) =>
        (await reader.HydrateAsync(new CommitmentDraft(tenantId, draftId), ct)
            .ConfigureAwait(false)).IsCreated;
}
