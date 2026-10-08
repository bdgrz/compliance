using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Reads platform draft classifications within a currently authorized client's administration context.</summary>
[Discriminator("bdgrz.independence.client-rules.get", 1)]
public sealed record GetClientIndependenceRules(Uuid TenantId)
    : IRequest<IReadOnlyList<IndependenceRuleVersionView>>, IIndependenceAdministrationRequest, ICallable;
