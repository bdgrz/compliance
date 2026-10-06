using System.Globalization;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>The post-staging lifecycle for one tenant-declared application source.</summary>
public sealed partial class ApplicationImportLedger : Aggregate
{
    readonly Uuid _tenantId;
    readonly string _sourceKey;
    readonly string _sourceNamespace;
    readonly Dictionary<Uuid, long> _canceledRevisions = [];
    readonly Dictionary<Uuid, ulong> _canceledEventVersions = [];
    readonly Dictionary<Uuid, long> _revisions = [];
    readonly Dictionary<(Uuid BatchId, Uuid RowId), ApplicationImportRowCorrelated> _correlations = [];

    public ApplicationImportLedger(Uuid tenantId, string sourceKey, string sourceNamespace)
        : base(IdFor(tenantId, sourceKey, sourceNamespace),
            new EventStreamAddress(tenantId.ToString(), "application_imports",
                IdFor(tenantId, sourceKey, sourceNamespace).ToString()))
    {
        _tenantId = tenantId;
        _sourceKey = sourceKey;
        _sourceNamespace = sourceNamespace;
        On<ApplicationImportCanceled>(ev =>
        {
            if (ev.TenantId != _tenantId)
                throw new InvalidOperationException("An import ledger event belongs to another tenant.");
            _canceledRevisions[ev.BatchId] = ev.Revision;
            _canceledEventVersions[ev.BatchId] = ev.Metadata.AggregateVersion;
            _revisions[ev.BatchId] = ev.Revision;
            if (_acceptingBatchId == ev.BatchId)
                _acceptingBatchId = null;
        });
        On<ApplicationImportRowCorrelated>(ev =>
        {
            if (ev.TenantId != _tenantId || ev.SourceKey != _sourceKey || ev.SourceNamespace != _sourceNamespace)
                throw new InvalidOperationException("An import correlation belongs to another source.");
            _revisions[ev.BatchId] = ev.Revision;
            _correlations[(ev.BatchId, ev.RowId)] = ev;
        });
        RegisterAcceptanceEvents();
    }

    public long? GetCanceledRevision(Uuid batchId) =>
        _canceledRevisions.TryGetValue(batchId, out var revision) ? revision : null;

    public bool IsCancellationDurable(Uuid batchId) =>
        _canceledEventVersions.TryGetValue(batchId, out var version) && CommittedStreamPosition >= version;

    public Result<IReadOnlyList<ApplicationImportPlannedRow>> PrepareAcceptancePlan(ImportBatch batch,
        long expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (!BelongsToSource(batch))
            return Result<IReadOnlyList<ApplicationImportPlannedRow>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The import batch was not found for this source."));
        if (batch.IsCanceled || GetCanceledRevision(batch.Id) is not null)
            return Result<IReadOnlyList<ApplicationImportPlannedRow>>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The import batch is canceled."));
        if (expectedRevision != GetRevision(batch))
            return Result<IReadOnlyList<ApplicationImportPlannedRow>>.Failure(
                VersionedRecordRules.StaleRevision("import", GetRevision(batch)).ToRequestError());
        var planned = new List<ApplicationImportPlannedRow>();
        foreach (var row in batch.GetRows())
        {
            var choice = GetCorrelation(batch.Id, row.RowId);
            if (row.ValidationFindings.Count > 0 || choice is null)
                return Result<IReadOnlyList<ApplicationImportPlannedRow>>.Failure(new RequestError(
                    RequestErrorKind.Conflict, "Every import row must be valid and explicitly correlated."));
            planned.Add(new ApplicationImportPlannedRow(row.RowId, row.SourceRecordId!, choice.Decision,
                choice.ApplicationId, choice.ExpectedApplicationRevision, row.Name!, row.Purpose!, row.OwnerReference));
        }
        return Result<IReadOnlyList<ApplicationImportPlannedRow>>.Success(planned.AsReadOnly());
    }

    public CommandFailure? Correlate(ImportBatch batch, CorrelateApplicationImportRow request,
        DeclaredApplication? application, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(request);
        if (!BelongsToSource(batch) || request.TenantId != _tenantId || request.BatchId != batch.Id)
            return CommandFailure.MissingRecord("The import batch was not found for this source.");
        var row = batch.GetRow(request.RowId);
        if (row is null)
            return CommandFailure.MissingRecord("The import row was not found.");
        if (batch.IsCanceled || GetCanceledRevision(batch.Id) is not null)
            return CommandFailure.StateConflict("The import batch is canceled.");
        if (row.ValidationFindings.Count > 0 || string.IsNullOrWhiteSpace(row.SourceRecordId))
            return CommandFailure.InvalidContent("An invalid or duplicate row cannot be correlated.");
        if (request.Decision is not ("link_existing" or "create_new") ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 2000 ||
            (request.Decision == "create_new" && (request.ApplicationId is not null || request.ExpectedApplicationRevision is not null)) ||
            (request.Decision == "link_existing" && (request.ApplicationId is null || request.ApplicationId == Uuid.Empty || request.ExpectedApplicationRevision is null or < 1)))
            return CommandFailure.InvalidContent("Correlation requires an explicit bounded decision and target revision for an existing application.");
        var revision = GetRevision(batch);
        var targetId = request.ApplicationId ?? Uuid.CreateVersion5(batch.Id, $"application_import_target:{row.RowId}");
        var previous = GetCorrelation(batch.Id, row.RowId);
        if (previous is not null && request.ExpectedBatchRevision > 0 && request.ExpectedBatchRevision <= revision &&
            previous.Decision == request.Decision && previous.ApplicationId == targetId &&
            previous.ExpectedApplicationRevision == request.ExpectedApplicationRevision && previous.Reason == request.Reason.Trim())
            return null;
        if (_frozenPlans.ContainsKey(batch.Id))
            return CommandFailure.StateConflict("The import plan is already frozen.");
        if (request.Decision == "link_existing")
        {
            if (application is null || !application.IsCreated || application.Stream.Realm != _tenantId.ToString() || application.Id != request.ApplicationId)
                return CommandFailure.MissingRecord("The application was not found.");
            if (application.IsRetired)
                return CommandFailure.StateConflict("The application is retired.");
            if (application.Revision != request.ExpectedApplicationRevision)
                return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("application", application.Revision));
        }
        if (request.ExpectedBatchRevision != revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("import", revision));
        RaiseEvent(new ApplicationImportRowCorrelated(_tenantId, _sourceKey, _sourceNamespace,
            batch.Id, revision + 1, row.RowId, row.SourceRecordId, request.Decision,
            targetId,
            request.ExpectedApplicationRevision, request.Reason.Trim(), actorMemberId, actorDisplay, recordedAt));
        return null;
    }

    public long GetRevision(ImportBatch batch) => Math.Max(batch.Revision, _revisions.GetValueOrDefault(batch.Id));

    public long? GetRecordedRevision(Uuid batchId) =>
        _revisions.TryGetValue(batchId, out var revision) ? revision : null;

    public ApplicationImportRowCorrelated? GetCorrelation(Uuid batchId, Uuid rowId) =>
        _correlations.GetValueOrDefault((batchId, rowId));

    public CommandFailure? Cancel(ImportBatch batch, long expectedRevision, string reason,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset canceledAt)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (!BelongsToSource(batch))
            return CommandFailure.MissingRecord("The import batch was not found for this source.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000)
            return CommandFailure.InvalidContent("Cancellation requires a reason of at most 2000 characters.");
        var canceledRevision = GetCanceledRevision(batch.Id);
        var revision = GetRevision(batch);
        if ((canceledRevision is not null || batch.IsCanceled) && expectedRevision <= revision)
            return null;
        if (expectedRevision != revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("import", revision));
        RaiseEvent(new ApplicationImportCanceled(_tenantId, batch.Id, revision + 1,
            reason.Trim(), actorMemberId, actorDisplay, canceledAt));
        return null;
    }

    bool BelongsToSource(ImportBatch batch) => batch.IsCreated && batch.Stream.Realm == _tenantId.ToString() &&
        StringComparer.Ordinal.Equals(batch.SourceKey, _sourceKey) && StringComparer.Ordinal.Equals(batch.SourceNamespace, _sourceNamespace);

    static Uuid IdFor(Uuid tenantId, string sourceKey, string sourceNamespace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceNamespace);
        if (tenantId == Uuid.Empty || sourceKey.Length > 128 || sourceNamespace.Length > 128 ||
            sourceKey != sourceKey.Trim() || sourceNamespace != sourceNamespace.Trim())
            throw new ArgumentException("An application import source requires a tenant and exact bounded identities.");
        return Uuid.CreateVersion5(tenantId,
            $"application_import_source:{sourceKey.Length.ToString(CultureInfo.InvariantCulture)}:{sourceKey}{sourceNamespace.Length.ToString(CultureInfo.InvariantCulture)}:{sourceNamespace}");
    }
}
