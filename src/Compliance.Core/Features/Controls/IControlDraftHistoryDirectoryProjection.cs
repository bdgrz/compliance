using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public interface IControlDraftHistoryDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
