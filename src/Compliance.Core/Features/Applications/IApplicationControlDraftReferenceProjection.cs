using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationControlDraftReferenceProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
