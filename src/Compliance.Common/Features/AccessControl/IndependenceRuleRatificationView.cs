using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record IndependenceRuleRatificationView(Uuid RatificationId, long RuleVersion,
    long ObservedRuleCatalogSequence, string RuleContentDigest, string SourceReference,
    Uuid StaffMemberId, Uuid UserId, long DirectoryStaffRevision,
    Uuid DutyDesignationId, long DutyRevision, ActorReference Actor, DateTimeOffset RecordedAt);
