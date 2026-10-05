using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public interface IControlEvaluationWorkItemProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);

    ValueTask<long> LoadRevisionAsync(Uuid tenantId, CancellationToken ct = default);
}
