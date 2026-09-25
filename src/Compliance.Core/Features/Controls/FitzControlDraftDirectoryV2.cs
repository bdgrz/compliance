using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

sealed class FitzControlDraftDirectoryV2(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/control-draft-directory-v2/projection",
            "ControlDraftDirectoryV2"),
        IControlDraftDirectoryReader, IControlDraftDirectoryProjection
{
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("ControlDraftDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString(), "controls")), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case ControlDraftCreated created:
                await ControlDraftDirectoryV2Schema.Controls.InsertAsync(Transaction,
                    new ControlDraftView(created.TenantId, created.ProgramId, created.ControlId,
                        created.Identifier, 1, "draft", OwnerResolution(created.Content),
                        ApplicabilityResolution(created.Content),
                        created.Content, created.ActorMemberId, created.ActorDisplay,
                        created.ChangedAt)
                    {
                        LastChangedBy = created.Actor,
                    }, ct).ConfigureAwait(false);
                await ControlDraftDirectoryV2Schema.Revisions.InsertAsync(Transaction,
                        new ControlDraftRevisionView(created.TenantId, created.ProgramId,
                            created.ControlId, created.Identifier, 1, created.Content,
                            created.ActorMemberId, created.ActorDisplay, created.ChangedAt)
                        {
                            Actor = created.Actor,
                        }, ct)
                    .ConfigureAwait(false);
                break;
            case ControlDraftRevised revised:
                var current = await ControlDraftDirectoryV2Schema.Controls.GetAsync(Transaction,
                    revised.ControlId, ct).ConfigureAwait(false);
                if (current is null || current.TenantId != revised.TenantId ||
                    current.ProgramId != revised.ProgramId ||
                    current.Revision + 1 != revised.Revision)
                    throw new InvalidOperationException(
                        "A control draft revision cannot project before its predecessor.");
                await ControlDraftDirectoryV2Schema.Controls.ReplaceAsync(Transaction, current,
                    current with
                    {
                        Revision = revised.Revision,
                        OwnerResolution = OwnerResolution(revised.Content),
                        ApplicabilityResolution = ApplicabilityResolution(revised.Content),
                        Content = revised.Content,
                        LastChangedByMemberId = revised.ActorMemberId,
                        LastChangedByDisplay = revised.ActorDisplay,
                        LastChangedBy = revised.Actor,
                        LastChangedAt = revised.ChangedAt,
                    }, ct).ConfigureAwait(false);
                await ControlDraftDirectoryV2Schema.Revisions.InsertAsync(Transaction,
                    new ControlDraftRevisionView(revised.TenantId, revised.ProgramId,
                        revised.ControlId, current.Identifier, revised.Revision,
                        revised.Content, revised.ActorMemberId, revised.ActorDisplay,
                        revised.ChangedAt)
                    {
                        Actor = revised.Actor,
                    }, ct).ConfigureAwait(false);
                break;
            case ControlDraftDiscarded discarded:
                var discardCurrent = await ControlDraftDirectoryV2Schema.Controls.GetAsync(
                    Transaction, discarded.ControlId, ct).ConfigureAwait(false);
                if (discardCurrent is null || discardCurrent.TenantId != discarded.TenantId ||
                    discardCurrent.ProgramId != discarded.ProgramId ||
                    discardCurrent.Revision != discarded.Revision)
                    throw new InvalidOperationException(
                        "A control draft discard cannot project before its exact draft revision.");
                await ControlDraftDirectoryV2Schema.Controls.DeleteAsync(Transaction, discardCurrent, ct)
                    .ConfigureAwait(false);
                break;
        }
    }

    static string OwnerResolution(ControlDraftContent content) =>
        string.IsNullOrWhiteSpace(content.OwnerReference) ? "unresolved" : "declared_unverified";

    static string ApplicabilityResolution(ControlDraftContent content) =>
        content.Applicability is { Count: > 0 } references &&
        references.All(static reference => reference is { Unresolved: false })
            ? "declared" : "unresolved";

    public async ValueTask<ControlDraftView?> GetAsync(Uuid tenantId, Uuid controlId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ControlDraftDirectoryV2Schema.Controls.GetAsync(tx, controlId, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<ControlDraftView>> ListProgramAsync(Uuid tenantId,
        Uuid programId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ControlDraftDirectoryV2Schema.Controls.QueryAsync(tx,
            ControlDraftDirectoryV2Schema.ByProgramIdentifier.Query()
                .WithPrefix(programId.ToString()).Take(Math.Clamp(limit, 1, 200))
                .After(cursor), ct).ConfigureAwait(false);
    }

    public async ValueTask<ControlDraftRevisionView?> GetRevisionAsync(Uuid tenantId,
        Uuid controlId, long revision, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ControlDraftDirectoryV2Schema.Revisions.GetAsync(tx,
                ControlDraftDirectoryV2Schema.RevisionKey(controlId, revision), ct)
            .ConfigureAwait(false);
    }
}
