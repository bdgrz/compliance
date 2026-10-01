using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>A reconciliation decision bound to one checked canonical revision.</summary>
public sealed record WorkforceSourceDecision(string Outcome, string Note, long TargetRevision,
    ActorReference Actor, DateTimeOffset DecidedAt);
