using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed class ProgramBoundaryImpactContributor : IBoundaryImpactContributor
{
    public string Context => "programs";

    public ValueTask<Result<BoundaryImpactContribution>> ContributeAsync(BoundaryView boundary,
        IReadOnlyList<BoundaryChange> changes, CancellationToken ct)
    {
        IReadOnlyList<BoundaryAffectedRecord> records =
            boundary.LatestApprovedVersion is not null && changes.Count > 0
                ? [new BoundaryAffectedRecord(boundary.TenantId, Context,
                    "program", boundary.ProgramId,
                    "The approved scope used by this program would change.")]
                : [];
        return ValueTask.FromResult(Result<BoundaryImpactContribution>.Success(
            new BoundaryImpactContribution(Context, records, true)));
    }
}
