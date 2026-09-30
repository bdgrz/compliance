using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>A person exactly as accepted when a roster snapshot was frozen.</summary>
public sealed record FrozenPerson(Uuid PersonId, long Revision, string DisplayName,
    string? WorkEmail);
