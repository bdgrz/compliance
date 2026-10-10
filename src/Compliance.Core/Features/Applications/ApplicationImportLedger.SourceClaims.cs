using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed partial class ApplicationImportLedger
{
    /// <summary>Only durable commit markers establish claims. Raw rows and plans confer no authority.</summary>
    public IReadOnlyList<ApplicationImportSourceClaim> GetSourceClaims() => Array.AsReadOnly(
        _commits.Values.Where(marker => IsCommitDurable(marker.BatchId))
            .OrderBy(marker => _commitVersions[marker.BatchId])
            .SelectMany(marker => _frozenPlans[marker.BatchId].Rows.Select(row =>
                new ApplicationImportSourceClaim(_frozenPlans[marker.BatchId].Start, row, marker.CommittedAt)))
            .GroupBy(claim => claim.Observation.SourceRecordId, StringComparer.Ordinal)
            .Select(group => group.Last()).Where(claim => claim.Observation.Decision != "retire")
            .OrderBy(claim => claim.Observation.SourceRecordId, StringComparer.Ordinal)
            .ToArray());

    public ApplicationImportSourceClaim? GetSourceClaim(string sourceRecordId) =>
        GetSourceClaims().SingleOrDefault(claim => claim.Observation.SourceRecordId == sourceRecordId);

    /// <summary>Missing complete-source observations are proposals only; this does not retire targets.</summary>
    public Result<IReadOnlyList<ApplicationImportSourceClaim>> GetMissingSourceClaims(ImportBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (!BelongsToSource(batch))
            return Result<IReadOnlyList<ApplicationImportSourceClaim>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The import batch was not found for this source."));
        if (HasUndurableCommit())
            return Result<IReadOnlyList<ApplicationImportSourceClaim>>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The source commit must be durable before reconciliation.", isTransient: true));
        var present = batch.GetRows().Select(row => row.SourceRecordId).ToHashSet(StringComparer.Ordinal);
        return Result<IReadOnlyList<ApplicationImportSourceClaim>>.Success(batch.Coverage == "declared_complete"
            ? Array.AsReadOnly(GetSourceClaims().Where(claim => !present.Contains(claim.Observation.SourceRecordId)).ToArray())
            : Array.Empty<ApplicationImportSourceClaim>());
    }

    bool HasUndurableCommit() => _commitVersions.Values.Any(version => CommittedStreamPosition < version);

    bool ConflictsWithRecordedClaim(ApplicationImportPlannedRow row) => _commits.Keys.Any(batchId =>
        _frozenPlans[batchId].Rows.Any(previous => previous.SourceRecordId == row.SourceRecordId &&
            !(row.Decision == "retire" && row.RetirementSourceClaimId ==
                Uuid.CreateVersion5(Id, $"application_import_claim:{row.SourceRecordId}") &&
              previous.ApplicationId == row.ApplicationId) &&
            (previous.ApplicationId != row.ApplicationId || row.Decision != "link_existing")));
}
