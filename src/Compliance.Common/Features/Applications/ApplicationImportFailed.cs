using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application_import.failed", 1)]
public sealed record ApplicationImportFailed(Uuid TenantId, string SourceKey, string SourceNamespace,
    Uuid BatchId, long Revision, string PlanSha256, Uuid? RowId, string FailureCode,
    DateTimeOffset FailedAt) : DomainEvent;
