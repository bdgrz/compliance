using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public interface IProgramManagementRequest : IRequestBase
{
    Uuid TenantId { get; }
}
