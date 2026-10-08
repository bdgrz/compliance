using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>An authored, retained rule version. Authorship does not ratify production rules.</summary>
public sealed record IndependenceRuleVersionView(long Version, IndependenceRuleContent Content,
    ActorReference Actor, DateTimeOffset RecordedAt, bool IsRatified);
