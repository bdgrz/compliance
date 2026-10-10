using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Retains a rule-based preview without supplying partner sign-off or granting client access.</summary>
[Discriminator("bdgrz.independence.evaluate", 1)]
public sealed record EvaluateClientIndependence(Uuid TenantId,
    Uuid EvaluationId, long ExpectedSequence, long RuleSetVersion, DateOnly ExaminationPeriodStart)
    : IRequest<IndependenceEvaluationView>, IIndependenceAdministrationRequest, IClientManagementMutationRequest, ICallable;
