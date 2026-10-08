using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>One immutable staged observation, never an Application mutation.</summary>
public sealed class ImportBatch : Aggregate
{
    // Fitz's stream event frame uses a u16 length. Leave >11 KiB for the Portia
    // envelope, metadata, stream address and framing after this payload bound.
    public const int MaximumStagedPayloadBytes = 48 * 1024;
    readonly Uuid _tenantId;
    bool _created;
    bool _canceled;
    long _revision;
    string? _contentSha256;
    readonly Dictionary<Uuid, ApplicationImportStagedRow> _rows = [];

    internal ApplicationImportStaged? SourceObservation { get; private set; }
    public bool IsCreated => _created;
    public long Revision => _revision;
    public bool IsCanceled => _canceled;
    public string? SourceKey { get; private set; }
    public string? SourceNamespace { get; private set; }
    public string? ContentDigest => _contentSha256;
    public string? Coverage { get; private set; }
    public Uuid SubmitterMemberId { get; private set; }
    public string? SubmitterDisplay { get; private set; }

    public ImportBatch(Uuid tenantId, Uuid batchId)
        : base(batchId, new EventStreamAddress(tenantId.ToString(), "application_imports",
            batchId.ToString()))
    {
        _tenantId = tenantId;
        On<ApplicationImportStaged>(ev =>
        {
            SourceObservation = ev;
            _created = true;
            _revision = 1;
            _contentSha256 = ev.ContentSha256;
            SourceKey = ev.SourceKey;
            SourceNamespace = ev.SourceNamespace;
            Coverage = ev.Coverage;
            SubmitterMemberId = ev.ActorMemberId;
            SubmitterDisplay = ev.ActorDisplay;
            foreach (var row in ev.Rows)
                _rows.Add(row.RowId, row with { ValidationFindings = Array.AsReadOnly(row.ValidationFindings.ToArray()) });
        });
        On<ApplicationImportCanceled>(ev =>
        {
            _canceled = true;
            _revision = ev.Revision;
        });
    }

    public ApplicationImportStagedRow? GetRow(Uuid rowId) => _rows.GetValueOrDefault(rowId);

    public IReadOnlyList<ApplicationImportStagedRow> GetRows() =>
        Array.AsReadOnly(_rows.Values.OrderBy(row => row.RowNumber).ToArray());

    public static Uuid BatchIdFor(StageApplicationImport request) =>
        Uuid.CreateVersion5(request.TenantId,
            $"application_import:{Part(request.SourceKey)}{Part(request.SourceNamespace)}{request.SubmissionId}");

    public Result<ApplicationImportRegistration> Stage(StageApplicationImport request,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset submittedAt)
    {
        if (request.TenantId != _tenantId || request.SubmissionId == Uuid.Empty ||
            !ValidIdentity(request.SourceKey, 128) ||
            !ValidIdentity(request.SourceNamespace, 128) ||
            request.Coverage is not ("partial" or "declared_complete") ||
            request.Rows is null or { Count: < 1 or > 200 } ||
            request.Rows.Any(row => row is null || row.SourceRecordId is { Length: > 256 } ||
                row.Name is { Length: > 200 } || row.Purpose is { Length: > 2000 } ||
                row.OwnerReference is { Length: > 2000 }))
            return Result<ApplicationImportRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation, "An import requires a source, coverage, submission ID, and 1–200 bounded rows."));

        var digest = ContentSha256(request);
        if (_created)
            return _contentSha256 == digest
                ? Result<ApplicationImportRegistration>.Success(
                    new ApplicationImportRegistration(Id, _revision, digest))
                : Result<ApplicationImportRegistration>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The submission ID already exists with different content."));

        var duplicated = request.Rows.Where(row => !string.IsNullOrWhiteSpace(row.SourceRecordId))
            .GroupBy(row => row.SourceRecordId!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        var rows = request.Rows.Select((row, index) => new ApplicationImportStagedRow(
            Uuid.CreateVersion5(Id, (index + 1).ToString(CultureInfo.InvariantCulture)),
            index + 1, row.SourceRecordId, row.Name, row.Purpose, row.OwnerReference,
            Findings(row, duplicated))).ToArray();
        if (JsonSerializer.SerializeToUtf8Bytes(new ApplicationImportStaged(
                _tenantId, Id, request.SubmissionId, request.SourceKey,
                request.SourceNamespace, request.Coverage, digest, rows,
                actorMemberId, actorDisplay, submittedAt),
                ComplianceCoreJsonContext.Default.ApplicationImportStaged).Length >
            MaximumStagedPayloadBytes)
            return Result<ApplicationImportRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The import rows exceed the bounded stage payload size."));
        RaiseEvent(new ApplicationImportStaged(_tenantId, Id, request.SubmissionId,
            request.SourceKey, request.SourceNamespace, request.Coverage, digest, rows,
            actorMemberId, actorDisplay, submittedAt));
        return Result<ApplicationImportRegistration>.Success(
            new ApplicationImportRegistration(Id, 1, digest));
    }

    public CommandFailure? Cancel(long expectedRevision, string reason, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset canceledAt)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The import batch was not found.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000)
            return CommandFailure.InvalidContent(
                "Cancellation requires a reason of at most 2000 characters.");
        if (_canceled && expectedRevision <= _revision)
            return null;
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("import", _revision));
        RaiseEvent(new ApplicationImportCanceled(_tenantId, Id, _revision + 1,
            reason.Trim(), actorMemberId, actorDisplay, canceledAt));
        return null;
    }

    static string[] Findings(ApplicationImportInputRow row, HashSet<string> duplicated)
    {
        var findings = new List<string>();
        if (!ValidIdentity(row.SourceRecordId, 256))
            findings.Add("source_record_id_required");
        if (string.IsNullOrWhiteSpace(row.Name) || row.Name.Length > 200)
            findings.Add("name_required");
        if (string.IsNullOrWhiteSpace(row.Purpose) || row.Purpose.Length > 2000)
            findings.Add("purpose_required");
        if (row.OwnerReference is { Length: > 2000 })
            findings.Add("owner_reference_too_long");
        if (row.SourceRecordId is not null && duplicated.Contains(row.SourceRecordId))
            findings.Add("duplicate_source_record_id");
        return [.. findings];
    }

    static bool ValidIdentity(string? value, int max) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= max &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal);

    static string Part(string? value) =>
        $"{(value?.Length ?? -1).ToString(CultureInfo.InvariantCulture)}:{value}";

    static string ContentSha256(StageApplicationImport request)
    {
        var value = new StringBuilder();
        value.Append(Part(request.SourceKey)).Append(Part(request.SourceNamespace))
            .Append(Part(request.Coverage))
            .Append(request.Rows.Count.ToString(CultureInfo.InvariantCulture))
            .Append(':');
        foreach (var row in request.Rows)
            value.Append(Part(row.SourceRecordId)).Append(Part(row.Name?.Trim()))
                .Append(Part(row.Purpose?.Trim())).Append(Part(row.OwnerReference?.Trim()));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString())));
    }
}
