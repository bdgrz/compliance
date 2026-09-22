using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationControlDraftReferenceDirectory
{
    ValueTask<Page<ApplicationControlDraftReferenceView>> ListAsync(Uuid tenantId,
        string subjectType, Uuid recordId, int limit, string? cursor,
        CancellationToken ct = default);

    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
}

public interface IApplicationControlDraftReferenceProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}

/// <summary>Keeps the current indexed entries for one Control draft without tenant-wide scans.</summary>
sealed record ControlDraftReferenceState(Uuid TenantId, Uuid ControlId, Uuid ProgramId,
    string Identifier, long Revision, IReadOnlyList<Uuid> EntryIds);

static class ApplicationControlDraftReferenceSchema
{
    public static readonly KvDirectoryIndex<ApplicationControlDraftReferenceView> ByRecord = new(
        "by_governed_record", 1, static reference =>
            [reference.SubjectType, reference.GovernedRecordId.ToString(),
                reference.ControlId.ToString(), reference.EntryId.ToString()]);

    public static readonly KvDirectory<ApplicationControlDraftReferenceView, (Uuid ControlId,
        Uuid EntryId)> References = new("application_control_draft_references",
        ComplianceCoreJsonContext.Default.ApplicationControlDraftReferenceView,
        static reference => (reference.ControlId, reference.EntryId),
        static key => [key.ControlId.ToString(), key.EntryId.ToString()], [ByRecord]);

    public static readonly KvDirectory<ControlDraftReferenceState, Uuid> States = new(
        "control_draft_reference_states",
        ComplianceCoreJsonContext.Default.ControlDraftReferenceState,
        static state => state.ControlId, static id => [id.ToString()], []);
}

sealed class FitzApplicationControlDraftReferenceDirectory(IKvClient client)
    : FitzKvProjectionStore(client,
        "kv://bdgrz/application-control-draft-references-v1/projection",
        "ApplicationControlDraftReferencesV1"),
      IApplicationControlDraftReferenceDirectory, IApplicationControlDraftReferenceProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case ControlDraftCreated created:
                var initial = new ControlDraftReferenceState(created.TenantId, created.ControlId,
                    created.ProgramId, created.Identifier, 1, ReferenceIds(created.Content));
                await ApplicationControlDraftReferenceSchema.States.InsertAsync(Transaction,
                    initial, ct).ConfigureAwait(false);
                await InsertAsync(created.TenantId, initial, created.Content, ct)
                    .ConfigureAwait(false);
                break;
            case ControlDraftRevised revised:
                var revising = await RequireStateAsync(revised.ControlId, ct).ConfigureAwait(false);
                if (revising.TenantId != revised.TenantId || revising.ProgramId != revised.ProgramId ||
                    revising.Revision + 1 != revised.Revision)
                    throw new InvalidOperationException(
                        "A control draft reference revision is out of order.");
                await DeleteReferencesAsync(revising.ControlId, revising.EntryIds, ct)
                    .ConfigureAwait(false);
                var revisedState = revising with
                {
                    Revision = revised.Revision,
                    EntryIds = ReferenceIds(revised.Content),
                };
                await ApplicationControlDraftReferenceSchema.States.ReplaceAsync(Transaction,
                    revising, revisedState, ct).ConfigureAwait(false);
                await InsertAsync(revised.TenantId, revisedState, revised.Content, ct)
                    .ConfigureAwait(false);
                break;
            case ControlDraftDiscarded discarded:
                var discarding = await RequireStateAsync(discarded.ControlId, ct)
                    .ConfigureAwait(false);
                if (discarding.TenantId != discarded.TenantId ||
                    discarding.ProgramId != discarded.ProgramId ||
                    discarding.Revision != discarded.Revision)
                    throw new InvalidOperationException(
                        "A control draft reference discard is out of order.");
                await DeleteReferencesAsync(discarding.ControlId, discarding.EntryIds, ct)
                    .ConfigureAwait(false);
                await ApplicationControlDraftReferenceSchema.States.DeleteAsync(Transaction,
                    discarded.ControlId, ct).ConfigureAwait(false);
                break;
        }
    }

    static Uuid[] ReferenceIds(ControlDraftContent content) =>
        [.. (content.Applicability ?? []).Where(IsIndexed).Select(static reference => reference.EntryId)];

    static bool IsIndexed(ControlApplicabilityReference? reference) =>
        reference is { Unresolved: false, GovernedRecordId: { } recordId } &&
        recordId != Uuid.Empty && reference.SubjectType is "application" or "system_instance";

    async ValueTask<ControlDraftReferenceState> RequireStateAsync(Uuid controlId,
        CancellationToken ct) =>
        await ApplicationControlDraftReferenceSchema.States.GetAsync(Transaction, controlId, ct)
            .ConfigureAwait(false) ?? throw new InvalidOperationException(
                "A control draft reference change cannot project before its creation.");

    async ValueTask InsertAsync(Uuid tenantId, ControlDraftReferenceState state,
        ControlDraftContent content, CancellationToken ct)
    {
        foreach (var reference in (content.Applicability ?? []).Where(IsIndexed))
            await ApplicationControlDraftReferenceSchema.References.InsertAsync(Transaction,
                new ApplicationControlDraftReferenceView(tenantId, reference.SubjectType,
                    reference.GovernedRecordId!.Value, state.ProgramId, state.ControlId,
                    state.Identifier, state.Revision, reference.EntryId, reference.Subject,
                    reference.Rationale), ct).ConfigureAwait(false);
    }

    async ValueTask DeleteReferencesAsync(Uuid controlId, IReadOnlyList<Uuid> entryIds,
        CancellationToken ct)
    {
        foreach (var entryId in entryIds)
            await ApplicationControlDraftReferenceSchema.References.DeleteAsync(Transaction,
                (controlId, entryId), ct).ConfigureAwait(false);
    }

    public async ValueTask<Page<ApplicationControlDraftReferenceView>> ListAsync(Uuid tenantId,
        string subjectType, Uuid recordId, int limit, string? cursor,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ApplicationControlDraftReferenceSchema.References.QueryAsync(tx,
            ApplicationControlDraftReferenceSchema.ByRecord.Query()
                .WithPrefix(subjectType, recordId.ToString())
                .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("ApplicationControlDraftReferencesV1",
            EventStreamPattern.ForPattern(tenantId.ToString(), "controls")), ct);
}
