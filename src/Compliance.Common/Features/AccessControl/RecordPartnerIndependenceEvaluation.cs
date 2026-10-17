using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Records the record partner independence evaluation intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.access-control.record-partner-independence-evaluation", 1)]
public sealed record RecordPartnerIndependenceEvaluation(Uuid TenantId, Uuid EngagementId, Uuid EvaluationId, long RuleSetVersion, string Evaluation, string Rationale) : IRequest, IClientManagementMutationRequest, ICallable;
