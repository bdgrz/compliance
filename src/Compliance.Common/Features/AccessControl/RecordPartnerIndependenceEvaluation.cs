using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.service-engagement.partner-evaluation.record", 1)]
public sealed record RecordPartnerIndependenceEvaluation(Uuid TenantId, Uuid EngagementId,
    Uuid EvaluationId, long ExpectedSequence, long ExpectedEngagementRevision, long RuleVersion,
    string ExpectedRuleContentDigest, string ExpectedServiceHistoryDigest, string Rationale)
    : IRequest<PartnerIndependenceEvaluationView>, IPersonalEngagementPartnerRequest, ICallable;
