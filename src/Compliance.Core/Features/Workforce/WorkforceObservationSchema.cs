using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

static class WorkforceObservationSchema
{
    public static readonly KvDirectoryIndex<RosterRelationshipSnapshot> ByPerson = new(
        "by_person", 1,
        static snapshot => [snapshot.PersonId.ToString(), snapshot.RelationshipId.ToString()]);

    public static readonly KvDirectory<RosterRelationshipSnapshot, Uuid> Relationships = new(
        "roster-relationships", ComplianceCoreJsonContext.Default.RosterRelationshipSnapshot,
        static snapshot => snapshot.RelationshipId,
        static relationshipId => [relationshipId.ToString()], [ByPerson]);

    public static readonly KvDirectoryIndex<WorkforceObservationView> ByObservedAt = new(
        "by_observed_at", 1,
        static view => [Sortable(view.ObservedAt), view.ObservationId.ToString()]);

    public static readonly KvDirectoryIndex<WorkforceObservationView> ByKind = new(
        "by_kind", 1,
        static view => [view.Kind, Sortable(view.ObservedAt), view.ObservationId.ToString()]);

    public static readonly KvDirectory<WorkforceObservationView, Uuid> Observations = new(
        "observations", ComplianceCoreJsonContext.Default.WorkforceObservationView,
        static view => view.ObservationId,
        static observationId => [observationId.ToString()], [ByObservedAt, ByKind]);

    static string Sortable(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
}
