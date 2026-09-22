using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Versioning;

/// <summary>Each owning context contributes change impact in its own terms.</summary>
public interface IImpactContributor<TSubject, TChange, TContribution>
{
    string Context { get; }

    ValueTask<Result<TContribution>> ContributeAsync(TSubject subject,
        IReadOnlyList<TChange> changes, CancellationToken ct);
}
