using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Untrusted tenant-supplied source row. Staging does not create an Application.</summary>
public sealed record ApplicationImportInputRow(string? SourceRecordId, string? Name,
    string? Purpose, string? OwnerReference);

public sealed record ApplicationImportRegistration(Uuid BatchId, long Revision,
    string ContentSha256);

public sealed record ApplicationImportView(Uuid TenantId, Uuid BatchId, Uuid SubmissionId,
    string SourceKey, string SourceNamespace, string Coverage, string ContentSha256,
    long Revision, string State, Uuid SubmittedByMemberId, string SubmittedByDisplay,
    DateTimeOffset SubmittedAt, int RowCount, int InvalidCount, int PendingCount,
    int AppliedCount, int SkippedCount, int FailedCount, DateTimeOffset LastProgressAt);

public sealed record ApplicationImportRowView(Uuid TenantId, Uuid BatchId, Uuid RowId,
    int RowNumber, string? SourceRecordId, string? Name, string? Purpose,
    string? OwnerReference, IReadOnlyList<string> ValidationFindings,
    string ProcessingState, Uuid? ApplicationId);

/// <summary>Provisional only: no source-claim bindings exist in this staging slice.</summary>
public sealed record ApplicationImportPreviewRow(Uuid TenantId, Uuid BatchId, Uuid RowId,
    int RowNumber, string? SourceRecordId, string? Name, string? Purpose,
    string? OwnerReference, IReadOnlyList<string> ValidationFindings,
    string MatchState, IReadOnlyList<Uuid> CandidateApplicationIds,
    IReadOnlyList<string> ChangedFields, IReadOnlyList<string> AcceptanceBlockers);

[Discriminator("bdgrz.application_import.stage", 1)]
public sealed record StageApplicationImport(Uuid TenantId, Uuid SubmissionId,
    string SourceKey, string SourceNamespace, string Coverage,
    IReadOnlyList<ApplicationImportInputRow> Rows)
    : IRequest<ApplicationImportRegistration>, IApplicationInventoryRequest, ICallable;

[Discriminator("bdgrz.application_import.get", 1)]
public sealed record GetApplicationImport(Uuid TenantId, Uuid BatchId,
    long? MinimumRevision = null)
    : IRequest<ApplicationImportView>, IApplicationInventoryRequest, ICallable;

[Discriminator("bdgrz.application_import.rows.list", 1)]
public sealed record ListApplicationImportRows(Uuid TenantId, Uuid BatchId,
    int? Limit = null, string? Cursor = null, long? MinimumRevision = null)
    : IRequest<Page<ApplicationImportRowView>>, IApplicationInventoryRequest, ICallable;

[Discriminator("bdgrz.application_import.preview", 1)]
public sealed record PreviewApplicationImport(Uuid TenantId, Uuid BatchId,
    int? Limit = null, string? Cursor = null, long? MinimumRevision = null)
    : IRequest<Page<ApplicationImportPreviewRow>>, IApplicationInventoryRequest, ICallable;

public sealed record ApplicationImportStagedRow(Uuid RowId, int RowNumber,
    string? SourceRecordId, string? Name, string? Purpose, string? OwnerReference,
    IReadOnlyList<string> ValidationFindings);

[Discriminator("bdgrz.application_import.staged", 1)]
public sealed record ApplicationImportStaged(Uuid TenantId, Uuid BatchId,
    Uuid SubmissionId, string SourceKey, string SourceNamespace, string Coverage,
    string ContentSha256, IReadOnlyList<ApplicationImportStagedRow> Rows,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset SubmittedAt) : DomainEvent;
