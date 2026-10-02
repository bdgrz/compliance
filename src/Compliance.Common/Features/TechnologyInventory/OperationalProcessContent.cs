using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     A governed system activity or data-handling process (M0-D22). It is distinct from a
///     written policy procedure. The name identifies it among active processes.
/// </summary>
public sealed record OperationalProcessContent(string Name, string Purpose, Uuid OwnerPersonId,
    string? Inputs, string? Outputs, string Lifecycle);
