using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

public interface IArtifactRetentionAdminRequest : IRequestBase
{
    Uuid TenantId { get; }
}
