using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     One open unit of source work before accountability and visibility are applied.
///     <c>Excluded</c> lists members the source workflow's separation of duties rejects.
/// </summary>
public sealed record WorkCandidate(Uuid WorkItemId, string Kind, Uuid SourceId, Uuid? ControlId,
    Uuid? FindingId, string Summary, string Reason, DateOnly? DueOn, string? Materiality,
    string NextAction, string ActionPath, OperatingHolder Responsible, OperatingHolder? Backup,
    IReadOnlySet<Uuid> Excluded, DateTimeOffset CreatedAt)
{
    public static Uuid IdFor(Uuid sourceId, string kind) => Uuid.CreateVersion5(sourceId,
        "work:" + kind);
}
