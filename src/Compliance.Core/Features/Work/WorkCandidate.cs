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
    /// <summary>Source-management permission required in addition to the named operating duty.</summary>
    public Uuid? RequiredManagementProgramId { get; init; }

    public static Uuid IdFor(Uuid sourceId, string kind) => Uuid.CreateVersion5(sourceId,
        "work:" + kind);
}
