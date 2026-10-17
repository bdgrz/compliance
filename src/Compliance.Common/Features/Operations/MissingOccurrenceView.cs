using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Expected control occurrences with no accepted performance record.</summary>
public sealed record MissingOccurrenceView(Uuid TenantId, Uuid ProgramId, Uuid MissingOccurrenceId, Uuid ControlId, DateOnly DueOn, string Status);
