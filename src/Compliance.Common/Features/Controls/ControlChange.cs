using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed record ControlChange(string Field, string ChangeType, Uuid? EntryId,
    string? PreviousValue, string? ProposedValue);
