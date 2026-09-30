using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IAccessReviewScopeDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
