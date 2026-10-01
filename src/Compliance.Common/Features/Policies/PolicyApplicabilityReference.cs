using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>
///     A version-aware relationship from a policy version to what it governs: an
///     <c>application</c>, <c>system_instance</c>, <c>control</c>, <c>criterion</c>, <c>risk</c>,
///     <c>vendor</c>, <c>process</c>, or organizational <c>scope</c>. It supports navigation and
///     impact analysis only; it never counts as control coverage or operating evidence.
/// </summary>
public sealed record PolicyApplicabilityReference(string SubjectType, string Subject,
    Uuid? RecordId = null);
