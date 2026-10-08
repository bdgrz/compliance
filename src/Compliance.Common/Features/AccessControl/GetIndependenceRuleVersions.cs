using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.independence.rules.list", 1)]
public sealed record GetIndependenceRuleVersions : IRequest<IReadOnlyList<IndependenceRuleVersionView>>,
    IIndependenceRuleAdministrationRequest, ICallable;
