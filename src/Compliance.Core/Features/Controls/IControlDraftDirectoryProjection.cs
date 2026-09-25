using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public interface IControlDraftDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
