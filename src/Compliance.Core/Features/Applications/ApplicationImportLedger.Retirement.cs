using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed partial class ApplicationImportLedger
{
    readonly Dictionary<Uuid, ApplicationImportRetirementProposalStarted> _retirementStarts = [];
    readonly Dictionary<Uuid, List<ApplicationImportRetirementRow>> _retirementRows = [];
    readonly Dictionary<Uuid, ApplicationImportRetirementProposal> _retirementProposals = [];
    readonly Dictionary<Uuid, ulong> _retirementSealPositions = [];

    /// <summary>Returns only a durable proposal still bound to this exact source/lifecycle snapshot.</summary>
    public ApplicationImportRetirementProposal? GetRetirementProposal(ImportBatch batch) =>
        BelongsToSource(batch) && GetState(batch) == "preview_ready" &&
        _retirementProposals.TryGetValue(batch.Id, out var proposal) &&
        batch.Coverage == "declared_complete" && proposal.Start.ContentSha256 == batch.ContentDigest &&
        proposal.Start.PresentSourceRecordIds.SequenceEqual(batch.GetRows().Select(row => row.SourceRecordId), StringComparer.Ordinal) &&
        proposal.Revision == GetRevision(batch) && CommittedStreamPosition == _retirementSealPositions[batch.Id] &&
        Version == CommittedStreamPosition ? proposal : null;

    /// <summary>Freezes omissions only. It neither begins acceptance nor grants retirement authority.</summary>
    public Result FreezeRetirementProposal(ImportBatch batch, long expectedRevision, ulong expectedSourcePosition,
        IReadOnlyDictionary<Uuid, DeclaredApplication> targets, Uuid memberId, string memberDisplay,
        string reason, DateTimeOffset preparedAt)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(targets);
        if (!BelongsToSource(batch))
            return Failure(RequestErrorKind.NotFound, "The import batch was not found for this source.");
        if (memberId == Uuid.Empty || string.IsNullOrWhiteSpace(memberDisplay) || memberDisplay.Length > 256 ||
            string.IsNullOrWhiteSpace(reason) || reason.Length > 2000)
            return Failure(RequestErrorKind.Validation, "A retirement proposal requires a bounded attributable decision.");
        if (GetState(batch) != "preview_ready")
            return Failure(RequestErrorKind.Conflict, "The import batch is not available for a retirement proposal.");
        if (_retirementStarts.TryGetValue(batch.Id, out var existing))
            return GetRetirementProposal(batch) is not null && expectedRevision == existing.Revision - 1 &&
                expectedSourcePosition == existing.SourcePosition && existing.MemberId == memberId &&
                existing.MemberDisplay == memberDisplay.Trim() && existing.Reason == reason.Trim()
                ? Result.Success : Failure(RequestErrorKind.Conflict, "The retirement proposal changed or is stale.");
        if (expectedRevision != GetRevision(batch))
            return Result.Failure(VersionedRecordRules.StaleRevision("import", GetRevision(batch)).ToRequestError());
        if (expectedSourcePosition != CommittedStreamPosition || Version != CommittedStreamPosition ||
            HasUndurableCommit() || _acceptingBatchId is not null || batch.CommittedStreamPosition == 0)
            return Failure(RequestErrorKind.Conflict, "The source must be durable and unchanged before freezing omissions.", true);
        if (batch.Coverage != "declared_complete" || batch.GetRows().Any(row => row.ValidationFindings.Count > 0))
            return Failure(RequestErrorKind.Conflict, "Only a valid declared-complete batch can propose retirement.");
        var missing = GetMissingSourceClaims(batch).Value;
        if (missing.Count is < 1 or > 200)
            return Failure(RequestErrorKind.Validation, "A retirement proposal requires between 1 and 200 missing claims.");
        var claims = GetSourceClaims();
        if (missing.Any(claim => claims.Count(other => other.Observation.ApplicationId == claim.Observation.ApplicationId) != 1) ||
            HasPresentRetirementAlias(batch.Id, batch.GetRows().Select(row => row.SourceRecordId!), missing))
            return Failure(RequestErrorKind.Conflict, "A source alias cannot authorize retirement of a shared target.");
        foreach (var claim in missing)
        {
            var row = RetirementRow(claim);
            if (!targets.TryGetValue(row.ApplicationId, out var target) || !target.IsCreated ||
                target.Id != row.ApplicationId || target.Stream.Realm != _tenantId.ToString())
                return Failure(RequestErrorKind.NotFound, "The retirement target was not found.");
            if (target.IsRetired || target.Revision != row.ExpectedApplicationRevision || target.CheckPendingImportChanges() is not null)
                return Failure(RequestErrorKind.Conflict, "The retirement target changed or has unsettled effects.");
        }
        var revision = GetRevision(batch);
        var start = new ApplicationImportRetirementProposalStarted(_tenantId, _sourceKey, _sourceNamespace,
            batch.Id, ++revision, expectedSourcePosition, batch.ContentDigest!,
            Array.AsReadOnly(batch.GetRows().Select(row => row.SourceRecordId!).ToArray()), missing.Count,
            memberId, memberDisplay.Trim(), reason.Trim(), preparedAt);
        var rows = missing.Select(RetirementRow).ToArray();
        var events = rows.Select(row => new ApplicationImportRetirementRowFrozen(_tenantId, batch.Id, ++revision, row)).ToArray();
        var seal = new ApplicationImportRetirementProposalSealed(_tenantId, batch.Id, ++revision, RetirementHash(start, rows));
        if (JsonSerializer.SerializeToUtf8Bytes(start, ComplianceCoreJsonContext.Default.ApplicationImportRetirementProposalStarted).Length > ImportBatch.MaximumStagedPayloadBytes ||
            events.Any(row => JsonSerializer.SerializeToUtf8Bytes(row, ComplianceCoreJsonContext.Default.ApplicationImportRetirementRowFrozen).Length > ImportBatch.MaximumStagedPayloadBytes))
            return Failure(RequestErrorKind.Validation, "The retirement proposal exceeds its event payload bounds.");
        RaiseEvent(start);
        foreach (var row in events)
            RaiseEvent(row);
        RaiseEvent(seal);
        return Result.Success;
    }

    void RegisterRetirementEvents()
    {
        On<ApplicationImportRetirementProposalStarted>(ev =>
        {
            if (ev.TenantId != _tenantId || ev.SourceKey != _sourceKey || ev.SourceNamespace != _sourceNamespace ||
                ev.BatchId == Uuid.Empty || _retirementStarts.ContainsKey(ev.BatchId) || _canceledRevisions.ContainsKey(ev.BatchId) ||
                _planStarts.ContainsKey(ev.BatchId) || _acceptingBatchId is not null ||
                ev.Revision != _revisions.GetValueOrDefault(ev.BatchId, 1) + 1 || ev.RowCount is < 1 or > 200 ||
                ev.SourcePosition != ev.Metadata.AggregateVersion - 1 || ev.MemberId == Uuid.Empty ||
                string.IsNullOrWhiteSpace(ev.MemberDisplay) || ev.MemberDisplay.Length > 256 || ev.MemberDisplay != ev.MemberDisplay.Trim() ||
                string.IsNullOrWhiteSpace(ev.Reason) || ev.Reason.Length > 2000 || ev.Reason != ev.Reason.Trim() ||
                ev.ContentSha256 is not { Length: 64 } || !ev.ContentSha256.All(Uri.IsHexDigit) ||
                ev.PresentSourceRecordIds is null ||
                ev.PresentSourceRecordIds.Count is < 1 or > 200 ||
                ev.PresentSourceRecordIds.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 256 || id != id.Trim()) ||
                ev.PresentSourceRecordIds.Distinct(StringComparer.Ordinal).Count() != ev.PresentSourceRecordIds.Count ||
                JsonSerializer.SerializeToUtf8Bytes(ev, ComplianceCoreJsonContext.Default.ApplicationImportRetirementProposalStarted).Length > ImportBatch.MaximumStagedPayloadBytes ||
                RetirementClaims(ev).Length != ev.RowCount)
                throw new InvalidOperationException("The retirement proposal does not belong to an available source snapshot.");
            var snapshot = ev with { PresentSourceRecordIds = Array.AsReadOnly(ev.PresentSourceRecordIds.ToArray()) };
            snapshot.AttachMetadata(ev.Metadata);
            _retirementStarts.Add(ev.BatchId, snapshot);
            _retirementRows.Add(ev.BatchId, []);
            _revisions[ev.BatchId] = ev.Revision;
        });
        On<ApplicationImportRetirementRowFrozen>(ev =>
        {
            if (ev.TenantId != _tenantId || !_retirementRows.TryGetValue(ev.BatchId, out var rows) ||
                _retirementProposals.ContainsKey(ev.BatchId) || _canceledRevisions.ContainsKey(ev.BatchId) ||
                rows.Count >= _retirementStarts[ev.BatchId].RowCount ||
                ev.Metadata.AggregateVersion != _retirementStarts[ev.BatchId].SourcePosition + (ulong)rows.Count + 2 || ev.Revision != _revisions[ev.BatchId] + 1 ||
                ev.Row != RetirementRow(RetirementClaims(_retirementStarts[ev.BatchId])[rows.Count]))
                throw new InvalidOperationException("The frozen retirement row is altered, duplicated or out of order.");
            rows.Add(ev.Row);
            _revisions[ev.BatchId] = ev.Revision;
        });
        On<ApplicationImportRetirementProposalSealed>(ev =>
        {
            if (ev.TenantId != _tenantId || !_retirementRows.TryGetValue(ev.BatchId, out var rows) ||
                _retirementProposals.ContainsKey(ev.BatchId) || _canceledRevisions.ContainsKey(ev.BatchId) ||
                rows.Count != _retirementStarts[ev.BatchId].RowCount ||
                ev.Metadata.AggregateVersion != _retirementStarts[ev.BatchId].SourcePosition + (ulong)rows.Count + 2 || ev.Revision != _revisions[ev.BatchId] + 1 ||
                ev.ProposalSha256 != RetirementHash(_retirementStarts[ev.BatchId], rows))
                throw new InvalidOperationException("The retirement proposal is incomplete or altered.");
            _retirementProposals.Add(ev.BatchId, new(_retirementStarts[ev.BatchId], Array.AsReadOnly(rows.ToArray()), ev.Revision, ev.ProposalSha256));
            _retirementSealPositions.Add(ev.BatchId, ev.Metadata.AggregateVersion);
            _revisions[ev.BatchId] = ev.Revision;
        });
    }

    ApplicationImportSourceClaim[] RetirementClaims(ApplicationImportRetirementProposalStarted start)
    {
        // Replay validates against preceding commit events even while a whole stream is being hydrated.
        var claims = _commits.Values.OrderBy(marker => _commitVersions[marker.BatchId])
            .SelectMany(marker => _frozenPlans[marker.BatchId].Rows.Select(row =>
                new ApplicationImportSourceClaim(_frozenPlans[marker.BatchId].Start, row, marker.CommittedAt)))
            .GroupBy(claim => claim.Observation.SourceRecordId, StringComparer.Ordinal).Select(group => group.Last()).ToArray();
        var missing = claims.Where(claim => !start.PresentSourceRecordIds.Contains(claim.Observation.SourceRecordId, StringComparer.Ordinal))
            .OrderBy(claim => claim.Observation.SourceRecordId, StringComparer.Ordinal).ToArray();
        if (missing.Any(claim => claims.Count(other => other.Observation.ApplicationId == claim.Observation.ApplicationId) != 1) ||
            HasPresentRetirementAlias(start.BatchId, start.PresentSourceRecordIds, missing))
            throw new InvalidOperationException("A source alias cannot authorize retirement of a shared target.");
        return missing;
    }

    bool HasPresentRetirementAlias(Uuid batchId, IEnumerable<string> present,
        IEnumerable<ApplicationImportSourceClaim> missing)
    {
        var targets = missing.Select(claim => claim.Observation.ApplicationId).ToHashSet();
        var identities = present.ToHashSet(StringComparer.Ordinal);
        return _correlations.Values.Any(choice => choice.BatchId == batchId &&
            identities.Contains(choice.SourceRecordId) && targets.Contains(choice.ApplicationId));
    }

    ApplicationImportRetirementRow RetirementRow(ApplicationImportSourceClaim claim) => new(
        Uuid.CreateVersion5(Id, $"application_import_claim:{claim.Observation.SourceRecordId}"), claim.Observation.SourceRecordId,
        claim.Observation.ApplicationId, claim.Plan.BatchId, claim.Observation.ExpectedApplicationRevision ?? 1, claim.CommittedAt);

    static string RetirementHash(ApplicationImportRetirementProposalStarted start, IReadOnlyList<ApplicationImportRetirementRow> rows)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(start, ComplianceCoreJsonContext.Default.ApplicationImportRetirementProposalStarted));
        hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(rows, ComplianceCoreJsonContext.Default.IReadOnlyListApplicationImportRetirementRow));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    static Result Failure(RequestErrorKind kind, string message, bool transient = false) =>
        Result.Failure(new RequestError(kind, message, isTransient: transient));
}
