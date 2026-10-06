using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application_import.row_correlated", 1)]
public sealed record ApplicationImportRowCorrelated(Uuid TenantId, string SourceKey,
    string SourceNamespace, Uuid BatchId, long Revision, Uuid RowId, string SourceRecordId,
    string Decision, Uuid ApplicationId, long? ExpectedApplicationRevision, string Reason,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset RecordedAt) : DomainEvent;
