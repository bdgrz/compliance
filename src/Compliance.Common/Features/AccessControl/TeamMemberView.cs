using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record TeamMemberView(Uuid TeamId, Uuid MemberId);
