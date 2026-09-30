using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>An exact source record a finding or gap relied on, such as a mapping version.</summary>
public sealed record ReadinessSourceReference(string Kind, Uuid Id, string? Version);
