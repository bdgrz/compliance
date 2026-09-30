using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed class FitzAccessReviewScopeDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/access-review-scope-directory-v1/projection",
            AccessReviewScopeStreams.ProjectorName),
        IAccessReviewScopeDirectoryReader, IAccessReviewScopeDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is not AccessReviewScopeDecided ev)
            return;
        var decision = new AccessReviewScopeDecisionView(ev.TenantId, ev.ApplicationId,
            ev.SystemInstanceId, ev.SystemInstanceRevision, ev.DecisionId, ev.Sequence,
            ev.Decision, ev.Reason, ev.EffectiveFrom, ev.ReviewBy, ev.Actor, ev.DecidedAt,
            ev.SeparationOfDutiesWaiverId);
        var existing = await AccessReviewScopeDirectorySchema.Scopes.GetAsync(Transaction,
            ev.SystemInstanceId, ct).ConfigureAwait(false);
        if (existing is null)
        {
            if (ev.Sequence != 1)
                throw new InvalidOperationException(
                    "A scope decision cannot project before its predecessor.");
            await AccessReviewScopeDirectorySchema.Scopes.InsertAsync(Transaction,
                new AccessReviewScopeRecord(ev.TenantId, ev.ApplicationId, ev.SystemInstanceId,
                    [decision]), ct).ConfigureAwait(false);
            return;
        }
        // A replayed decision that is already projected is ignored.
        if (ev.Sequence <= existing.Decisions.Count)
            return;
        if (ev.Sequence != existing.Decisions.Count + 1)
            throw new InvalidOperationException(
                "A scope decision cannot project before its predecessor.");
        await AccessReviewScopeDirectorySchema.Scopes.ReplaceAsync(Transaction, existing,
            existing with { Decisions = [.. existing.Decisions, decision] }, ct)
            .ConfigureAwait(false);
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(AccessReviewScopeStreams.ProjectorName,
            AccessReviewScopeStreams.TenantPattern(tenantId)), ct);

    public async ValueTask<AccessReviewScopeRecord?> GetAsync(Uuid tenantId,
        Uuid systemInstanceId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await AccessReviewScopeDirectorySchema.Scopes.GetAsync(tx, systemInstanceId, ct)
            .ConfigureAwait(false);
    }
}
