using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record TeamMemberView(Uuid TeamId, Uuid MemberId, Uuid? UserId = null,
    string? DisplayName = null, string? VerifiedEmailAddress = null);
