using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

sealed class FitzCriteriaTextOverlayDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/criteria-text-overlay-directory-v1/projection",
        ProjectorName), ICriteriaTextOverlayDirectoryReader, ICriteriaTextOverlayDirectoryProjection
{
    public const string ProjectorName = "CriteriaTextOverlayDirectoryV1";
    Uuid? _batchTenantId;

    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context,
        CancellationToken ct = default) => BeginOverlayBatchAsync(context, ct);

    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context,
        CancellationToken ct) => BeginOverlayBatchAsync(context, ct);

    async ValueTask<IProjectionBatch> BeginOverlayBatchAsync(ProjectionBatchContext context,
        CancellationToken ct)
    {
        if (context.Identity.Pattern.Area != CriteriaTextOverlayLedger.Area ||
            context.Identity.Pattern.Resource is not null ||
            !Uuid.TryParse(context.Identity.Pattern.Realm, null, out var tenantId) ||
            tenantId == Uuid.Empty)
            throw new InvalidOperationException(
                "A criteria overlay projection batch requires its tenant's overlay area.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _batchTenantId = tenantId;
        return batch;
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName, EventStreamPattern.ForPattern(tenantId.ToString(),
            CriteriaTextOverlayLedger.Area)), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is not CriteriaTextOverlayEntryRevised revised ||
            revised.TenantId == Uuid.Empty || revised.EditionId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(revised.Identifier) || revised.Identifier.Length > 200 ||
            revised.Identifier.Any(char.IsControl) || revised.Revision < 1 ||
            revised.TenantId != _batchTenantId)
            throw new InvalidOperationException(
                "A criteria overlay event must belong to its tenant projection batch.");

        var key = CriteriaTextOverlayDirectorySchema.Key(revised.EditionId,
            revised.Identifier);
        var current = await CriteriaTextOverlayDirectorySchema.Overlays.GetAsync(Transaction,
            key, ct).ConfigureAwait(false);
        if (current is not null && (current.TenantId != revised.TenantId ||
            current.EditionId != revised.EditionId ||
            !string.Equals(current.Identifier, revised.Identifier, StringComparison.Ordinal)))
            throw new InvalidOperationException(
                "A criteria overlay projection key cannot cross its tenant or criterion.");

        if (current is not null && revised.Revision < current.Revision)
            return;
        if (current is not null && revised.Revision == current.Revision)
        {
            if (!Same(current, revised))
                throw new InvalidOperationException(
                    "A criteria overlay revision cannot replace retained content.");
            return;
        }
        if (current is null ? revised.Revision != 1 :
            revised.Revision != current.Revision + 1)
            throw new InvalidOperationException(
                "A criteria overlay revision requires its immediate predecessor.");

        var next = new CriteriaTextOverlayRevision(revised.TenantId, revised.EditionId,
            revised.Identifier, revised.Revision, revised.Content, revised.Actor,
            revised.RecordedAt);
        if (current is null)
            await CriteriaTextOverlayDirectorySchema.Overlays.InsertAsync(Transaction, next, ct)
                .ConfigureAwait(false);
        else
            await CriteriaTextOverlayDirectorySchema.Overlays.ReplaceAsync(Transaction, current,
                next, ct).ConfigureAwait(false);
    }

    public async ValueTask<Result<IReadOnlyDictionary<string, CriteriaTextOverlayRevision>>>
        GetManyAsync(Uuid tenantId, Uuid editionId, IReadOnlyList<string> identifiers,
            CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var overlays = new Dictionary<string, CriteriaTextOverlayRevision>(StringComparer.Ordinal);
        foreach (var identifier in identifiers.Distinct(StringComparer.Ordinal))
        {
            var overlay = await CriteriaTextOverlayDirectorySchema.Overlays.GetAsync(tx,
                CriteriaTextOverlayDirectorySchema.Key(editionId, identifier), ct)
                .ConfigureAwait(false);
            if (overlay is null)
                continue;
            if (overlay.TenantId != tenantId || overlay.EditionId != editionId ||
                !string.Equals(overlay.Identifier, identifier, StringComparison.Ordinal))
                return Result<IReadOnlyDictionary<string, CriteriaTextOverlayRevision>>.Failure(
                    new RequestError(RequestErrorKind.Conflict,
                        "The criteria overlay projection has an invalid scope.",
                        isTransient: true));
            overlays.Add(identifier, overlay);
        }
        return Result<IReadOnlyDictionary<string, CriteriaTextOverlayRevision>>.Success(overlays);
    }

    static bool Same(CriteriaTextOverlayRevision current,
        CriteriaTextOverlayEntryRevised revised) =>
        current.TenantId == revised.TenantId && current.EditionId == revised.EditionId &&
        string.Equals(current.Identifier, revised.Identifier, StringComparison.Ordinal) &&
        current.Revision == revised.Revision && current.Content == revised.Content &&
        current.Actor == revised.Actor && current.RecordedAt == revised.RecordedAt;
}
