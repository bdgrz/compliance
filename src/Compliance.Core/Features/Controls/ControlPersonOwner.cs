using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     A workforce person owning one exact control revision, with the member the person is
///     correlated with, if any. A designation records the snapshot; activation passes the person's
///     current, roster-verified correlation.
/// </summary>
public sealed record ControlPersonOwner(Uuid PersonId, Uuid? CorrelatedMemberId,
    Uuid DesignationId = default);
