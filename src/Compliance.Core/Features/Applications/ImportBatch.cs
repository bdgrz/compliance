using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    string? _contentSha256;

    public bool IsCreated => _created;
    public long Revision => _created ? 1 : 0;

    public ImportBatch(Uuid tenantId, Uuid batchId)
        : base(batchId, new EventStreamAddress(tenantId.ToString(), "application_imports",
            batchId.ToString()))
    {
        _tenantId = tenantId;
        On<ApplicationImportStaged>(ev =>
        {
            _created = true;
            _contentSha256 = ev.ContentSha256;
        });
    }

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
                    new ApplicationImportRegistration(Id, 1, digest))
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
