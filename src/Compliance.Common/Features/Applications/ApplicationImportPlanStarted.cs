using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application_import.plan_started", 1)]
public sealed record ApplicationImportPlanStarted(Uuid TenantId, string SourceKey,
    string SourceNamespace, Uuid BatchId, long Revision, string ContentSha256,
    string Coverage, int RowCount, Uuid SubmitterMemberId, string SubmitterDisplay,
    Uuid ApproverMemberId, string ApproverDisplay, DateTimeOffset StartedAt) : DomainEvent;
