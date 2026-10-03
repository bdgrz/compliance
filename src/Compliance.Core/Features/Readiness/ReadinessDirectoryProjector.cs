using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

public sealed partial class ReadinessDirectoryProjector(
    IReadinessDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(ReadinessLedger.Area),
        FitzReadinessDirectory.ProjectorName),
    IProjectorHandler<ReadinessAssessmentRecorded>,
    IProjectorHandler<ReadinessGapPlanned>,
    IProjectorHandler<ReadinessDecisionRecorded>,
    IProjectorHandler<TypeIEntryDecisionRecorded>,
    IProjectorHandler<ReadinessGapAnnotated>
{
    public ValueTask HandleAsync(ReadinessAssessmentRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ReadinessGapPlanned ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ReadinessDecisionRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TypeIEntryDecisionRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ReadinessGapAnnotated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
