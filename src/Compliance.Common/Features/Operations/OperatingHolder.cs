using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Who holds an operating responsibility: a member, a workforce person who may never sign in,
///     or a team. A team may hold a responsibility but is never the performer or approver of record.
/// </summary>
public sealed record OperatingHolder(string Kind, Uuid Id);
