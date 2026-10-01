using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

static class WorkforceSourceSchema
{
    public static readonly KvDirectoryIndex<WorkforceSourceView> ByObservedAt = new("by_observed_at", 1,
        static view => [view.ObservedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), view.ObservationId.ToString()]);

    public static readonly KvDirectoryIndex<WorkforceSourceView> ByTarget = new("by_target", 1,
        static view => [view.TargetKind, view.TargetId.ToString(), view.ObservationId.ToString()]);

    public static readonly KvDirectory<WorkforceSourceView, Uuid> Observations = new("observations",
        ComplianceCoreJsonContext.Default.WorkforceSourceView, static view => view.ObservationId,
        static id => [id.ToString()], [ByObservedAt, ByTarget]);
}
