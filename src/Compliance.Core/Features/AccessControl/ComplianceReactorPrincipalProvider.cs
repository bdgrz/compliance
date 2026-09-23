using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Attributes each reactor's effects to its stable registered workload name.</summary>
sealed class ComplianceReactorPrincipalProvider : IReactorPrincipalProvider
{
    public ClaimsPrincipal GetPrincipal(Reactor reactor)
    {
        ArgumentNullException.ThrowIfNull(reactor);
        return RequestActor.CreateSystem($"reactor:{reactor.Name}", "bdgrz.system");
    }
}
