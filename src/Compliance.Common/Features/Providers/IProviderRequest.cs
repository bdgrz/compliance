using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public interface IProviderRequest : IRequestBase
{
    Uuid TenantId { get; }
}
