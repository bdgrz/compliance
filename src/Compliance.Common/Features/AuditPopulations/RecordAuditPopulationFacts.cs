using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records the record audit population facts intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.audit-populations.record-audit-population-facts", 1)]
public sealed record RecordAuditPopulationFacts(Uuid TenantId, Uuid ProgramId, Uuid DefinitionId, IReadOnlyList<Uuid> SourceFactIds, string SourceReference, string CompletenessEvidence) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
