using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.independence.history.get", 1)]
public sealed record GetClientIndependenceHistory(Uuid TenantId)
    : IRequest<IndependenceHistoryView>, IIndependenceAdministrationRequest, ICallable;
