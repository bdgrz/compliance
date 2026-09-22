using System.Globalization;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public interface IControlDraftDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
    ValueTask<ControlDraftView?> GetAsync(Uuid tenantId, Uuid controlId,
        CancellationToken ct = default);
    ValueTask<Page<ControlDraftView>> ListProgramAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default);
    ValueTask<ControlDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid controlId,
        long revision, CancellationToken ct = default);
}

public interface IControlDraftDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}

static class ControlDraftDirectoryV2Schema
{
    public static readonly KvDirectoryIndex<ControlDraftView> ByProgramIdentifier = new(
        "by_program_identifier", 1,
        static control => [control.ProgramId.ToString(), control.Identifier]);

    public static readonly KvDirectory<ControlDraftView, Uuid> Controls = new(
        "control_drafts", ComplianceCoreJsonContext.Default.ControlDraftView,
        static control => control.ControlId,
        static controlId => [controlId.ToString()], [ByProgramIdentifier]);

    public static readonly KvDirectory<ControlDraftRevisionView, string> Revisions = new(
        "control_draft_revisions", ComplianceCoreJsonContext.Default.ControlDraftRevisionView,
        static revision => RevisionKey(revision.ControlId, revision.Revision),
        static key => [key], []);

    public static string RevisionKey(Uuid controlId, long revision) =>
        $"{controlId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";
}

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
                        created.ChangedAt), ct).ConfigureAwait(false);
                await ControlDraftDirectoryV2Schema.Revisions.InsertAsync(Transaction,
                    new ControlDraftRevisionView(created.TenantId, created.ProgramId,
                        created.ControlId, created.Identifier, 1, created.Content,
                        created.ActorMemberId, created.ActorDisplay, created.ChangedAt), ct)
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
                        LastChangedAt = revised.ChangedAt,
                    }, ct).ConfigureAwait(false);
                await ControlDraftDirectoryV2Schema.Revisions.InsertAsync(Transaction,
                    new ControlDraftRevisionView(revised.TenantId, revised.ProgramId,
                        revised.ControlId, current.Identifier, revised.Revision,
                        revised.Content, revised.ActorMemberId, revised.ActorDisplay,
                        revised.ChangedAt), ct).ConfigureAwait(false);
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
