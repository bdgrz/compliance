using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

static class PersonDirectorySchema
{
    public static readonly KvDirectoryIndex<PersonView> ByDisplayName = new(
        "by_display_name", 1,
        static person => [person.DisplayName.ToUpperInvariant(), person.PersonId.ToString()]);

    public static readonly KvDirectory<PersonView, Uuid> People = new(
        "people", ComplianceCoreJsonContext.Default.PersonView,
        static person => person.PersonId,
        static personId => [personId.ToString()], [ByDisplayName]);
}
