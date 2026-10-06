using System.Globalization;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>The post-staging lifecycle for one tenant-declared application source.</summary>
public sealed class ApplicationImportLedger : Aggregate
{
    readonly Uuid _tenantId;
    readonly string _sourceKey;
    readonly string _sourceNamespace;
    readonly Dictionary<Uuid, long> _canceledRevisions = [];

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
        });
    }

    public long? GetCanceledRevision(Uuid batchId) =>
        _canceledRevisions.TryGetValue(batchId, out var revision) ? revision : null;

    public CommandFailure? Cancel(ImportBatch batch, long expectedRevision, string reason,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset canceledAt)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (!batch.IsCreated || batch.Stream.Realm != _tenantId.ToString() ||
            !StringComparer.Ordinal.Equals(batch.SourceKey, _sourceKey) ||
            !StringComparer.Ordinal.Equals(batch.SourceNamespace, _sourceNamespace))
            return CommandFailure.MissingRecord("The import batch was not found for this source.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000)
            return CommandFailure.InvalidContent("Cancellation requires a reason of at most 2000 characters.");
        var canceledRevision = GetCanceledRevision(batch.Id);
        var revision = canceledRevision ?? batch.Revision;
        if ((canceledRevision is not null || batch.IsCanceled) && expectedRevision <= revision)
            return null;
        if (expectedRevision != revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("import", revision));
        RaiseEvent(new ApplicationImportCanceled(_tenantId, batch.Id, revision + 1,
            reason.Trim(), actorMemberId, actorDisplay, canceledAt));
        return null;
    }

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
