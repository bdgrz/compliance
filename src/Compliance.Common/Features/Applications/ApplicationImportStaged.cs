using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application_import.staged", 1)]
public sealed record ApplicationImportStaged(Uuid TenantId, Uuid BatchId,
    Uuid SubmissionId, string SourceKey, string SourceNamespace, string Coverage,
    string ContentSha256, IReadOnlyList<ApplicationImportStagedRow> Rows,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset SubmittedAt) : DomainEvent;
