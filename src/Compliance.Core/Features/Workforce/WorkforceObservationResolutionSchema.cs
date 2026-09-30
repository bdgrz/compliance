using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

static class WorkforceObservationResolutionSchema
{
    public static readonly KvDirectory<WorkforceObservationResolutionView, Uuid> Resolutions = new(
        "resolutions", ComplianceCoreJsonContext.Default.WorkforceObservationResolutionView,
        static view => view.ObservationId,
        static observationId => [observationId.ToString()], []);
}
