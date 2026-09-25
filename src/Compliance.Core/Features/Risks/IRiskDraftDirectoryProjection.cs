using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public interface IRiskDraftDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
