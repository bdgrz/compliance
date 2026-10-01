using System.Globalization;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     The canonical v1 row format of a frozen workforce roster: one <c>person/…</c> row per person
///     and one <c>work_relationship/…</c> row per relationship, ordered by key. Rows hold the full
///     accepted facts, including the restricted manager chain; reads redact it.
/// </summary>
static class WorkforceRosterSnapshotContent
{
    public const string Kind = "workforce_roster";
    const string PersonPrefix = "person/";
    const string RelationshipPrefix = "work_relationship/";

    public static IReadOnlyList<PopulationRow> Rows(IEnumerable<PersonView> people,
        IEnumerable<WorkRelationshipView> relationships)
    {
        var rows = new List<PopulationRow>();
        foreach (var person in people)
            rows.Add(new PopulationRow(PersonPrefix + person.PersonId, Element(writer =>
            {
                writer.WriteString("person_id", person.PersonId.ToString());
                writer.WriteNumber("revision", person.Revision);
                writer.WriteString("display_name", person.DisplayName);
                WriteNullable(writer, "work_email", person.WorkEmail);
            })));
        foreach (var job in relationships)
            rows.Add(new PopulationRow(RelationshipPrefix + job.SourceWorkerId, Element(writer =>
            {
                writer.WriteString("relationship_id", job.RelationshipId.ToString());
                writer.WriteNumber("revision", job.Revision);
                writer.WriteString("person_id", job.PersonId.ToString());
                writer.WriteString("source_worker_id", job.SourceWorkerId);
                writer.WriteString("worker_type", job.WorkerType);
                writer.WriteString("lifecycle_status", job.LifecycleStatus);
                writer.WriteString("start_date", Date(job.StartDate));
                WriteNullable(writer, "end_date", job.EndDate is { } end ? Date(end) : null);
                WriteNullable(writer, "department", job.Department);
                WriteNullable(writer, "manager_person_id", job.ManagerPersonId?.ToString());
                WriteNullable(writer, "sponsor_person_id", job.SponsorPersonId?.ToString());
            })));
        rows.Sort(static (left, right) =>
            SnapshotContentIdentity.CompareCodePoints(
                SnapshotContentIdentity.NormalizeText(left.Key),
                SnapshotContentIdentity.NormalizeText(right.Key)));
        return rows;
    }

    public static WorkforceRosterSnapshotView ToView(Uuid tenantId, PopulationSnapshot snapshot,
        IReadOnlyList<PopulationRow> rows, FieldRedactor managerChain)
    {
        var people = new List<FrozenPerson>();
        var relationships = new List<FrozenWorkRelationship>();
        foreach (var row in rows)
        {
            var content = row.Content;
            if (row.Key.StartsWith(PersonPrefix, StringComparison.Ordinal))
                people.Add(new FrozenPerson(Id(content, "person_id"),
                    content.GetProperty("revision").GetInt64(), Text(content, "display_name")!,
                    Text(content, "work_email")));
            else if (row.Key.StartsWith(RelationshipPrefix, StringComparison.Ordinal))
                relationships.Add(new FrozenWorkRelationship(Id(content, "relationship_id"),
                    content.GetProperty("revision").GetInt64(), Id(content, "person_id"),
                    Text(content, "source_worker_id")!, Text(content, "worker_type")!,
                    Text(content, "lifecycle_status")!, ParseDate(Text(content, "start_date"))!.Value,
                    ParseDate(Text(content, "end_date")), Text(content, "department"),
                    managerChain.CanRead ? OptionalId(content, "manager_person_id") : null,
                    OptionalId(content, "sponsor_person_id")));
            else
                throw new SnapshotContentException("The roster snapshot holds an unknown row kind.");
        }
        return new WorkforceRosterSnapshotView(tenantId, snapshot.Id, snapshot.RootSnapshotId,
            snapshot.AmendsSnapshotId, snapshot.ContentSha256!, snapshot.RowCount,
            snapshot.AmendmentReason, snapshot.FrozenBy!, snapshot.FrozenAt, people, relationships,
            true);
    }

    static JsonElement Element(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }
        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    static void WriteNullable(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null)
            writer.WriteNull(name);
        else
            writer.WriteString(name, value);
    }

    static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    static DateOnly? ParseDate(string? value) => value is null
        ? null
        : DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    static string? Text(JsonElement content, string name) =>
        content.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    static Uuid Id(JsonElement content, string name) => Uuid.Parse(Text(content, name)!, CultureInfo.InvariantCulture);

    static Uuid? OptionalId(JsonElement content, string name) =>
        Text(content, name) is { } value ? Uuid.Parse(value, CultureInfo.InvariantCulture) : null;
}
