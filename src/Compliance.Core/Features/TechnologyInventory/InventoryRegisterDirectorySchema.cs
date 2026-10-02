using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

static class InventoryRegisterDirectorySchema
{
    public static readonly KvDirectoryIndex<LocationView> LocationsByName = new("by_name", 1,
        static view => [view.Content.Name.ToUpperInvariant(), view.LocationId.ToString()]);

    public static readonly KvDirectory<LocationView, Uuid> Locations = new("locations",
        ComplianceCoreJsonContext.Default.LocationView, static view => view.LocationId,
        static id => [id.ToString()], [LocationsByName]);

    public static readonly KvDirectoryIndex<LocationView> LocationRevisionsById = new(
        "by_location", 1, static view => [view.LocationId.ToString(), Padded(view.Revision)]);

    public static readonly KvDirectory<LocationView, string> LocationRevisions = new(
        "location_revisions", ComplianceCoreJsonContext.Default.LocationView,
        static view => RevisionKey(view.LocationId, view.Revision), static key => [key],
        [LocationRevisionsById]);

    public static readonly KvDirectoryIndex<OperationalProcessView> ProcessesByName = new(
        "by_name", 1, static view =>
            [view.Content.Name.ToUpperInvariant(), view.OperationalProcessId.ToString()]);

    public static readonly KvDirectory<OperationalProcessView, Uuid> Processes = new(
        "operational_processes", ComplianceCoreJsonContext.Default.OperationalProcessView,
        static view => view.OperationalProcessId, static id => [id.ToString()],
        [ProcessesByName]);

    public static readonly KvDirectoryIndex<OperationalProcessView> ProcessRevisionsById = new(
        "by_process", 1, static view =>
            [view.OperationalProcessId.ToString(), Padded(view.Revision)]);

    public static readonly KvDirectory<OperationalProcessView, string> ProcessRevisions = new(
        "operational_process_revisions",
        ComplianceCoreJsonContext.Default.OperationalProcessView,
        static view => RevisionKey(view.OperationalProcessId, view.Revision), static key => [key],
        [ProcessRevisionsById]);

    public static string RevisionKey(Uuid id, long revision) => $"{id}:{Padded(revision)}";

    static string Padded(long revision) =>
        revision.ToString("D20", CultureInfo.InvariantCulture);
}
