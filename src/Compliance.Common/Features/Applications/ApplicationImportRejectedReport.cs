using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Bounded report derived from retained raw rows and the durable lifecycle; no target fields.</summary>
public sealed record ApplicationImportRejectedReport(Uuid TenantId, Uuid BatchId, long Revision, string State,
    IReadOnlyList<ApplicationImportRejectedRow> Rows, IReadOnlyList<string> BatchFindings, string? FailureCode, Uuid? FailedRowId);
