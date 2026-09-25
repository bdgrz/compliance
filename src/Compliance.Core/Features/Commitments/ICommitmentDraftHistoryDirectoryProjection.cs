using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public interface ICommitmentDraftHistoryDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
