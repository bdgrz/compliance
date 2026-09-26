using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public interface IWorkforceRequest : IRequestBase
{
    Uuid TenantId { get; }
}
