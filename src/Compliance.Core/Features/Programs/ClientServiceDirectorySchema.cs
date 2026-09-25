using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

static class ClientServiceDirectorySchema
{
    public static readonly KvDirectoryIndex<ClientServiceView> ByName = new(
        "by_name", 1, static service => [service.Name.ToUpperInvariant()]);

    public static readonly KvDirectoryIndex<ClientServiceView> ByProgram = new(
        "by_program", 1, static service =>
            [(service.ProgramId ?? Uuid.Empty).ToString(), service.Name.ToUpperInvariant(),
                service.ServiceId.ToString()]);

    public static readonly KvDirectory<ClientServiceView, Uuid> Directory = new(
        "client_services", ComplianceCoreJsonContext.Default.ClientServiceView,
        static service => service.ServiceId,
        static serviceId => [serviceId.ToString()], [ByName, ByProgram]);

    public static readonly KvDirectoryIndex<ClientServiceRevisionView> RevisionsByService = new(
        "by_service", 1,
        static revision => [revision.ServiceId.ToString(),
            revision.Revision.ToString("D20", CultureInfo.InvariantCulture)]);

    public static readonly KvDirectory<ClientServiceRevisionView, string> Revisions = new(
        "client_service_revisions", ComplianceCoreJsonContext.Default.ClientServiceRevisionView,
        static revision => $"{revision.ServiceId}:{revision.Revision.ToString("D20", CultureInfo.InvariantCulture)}",
        static key => [key], [RevisionsByService]);
}
