using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application_import.committed", 1)]
public sealed record ApplicationImportCommitted(Uuid TenantId, string SourceKey,
    string SourceNamespace, Uuid BatchId, long Revision, string PlanSha256,
    IReadOnlyList<ApplicationImportEffectProof> Effects, DateTimeOffset CommittedAt) : DomainEvent;
