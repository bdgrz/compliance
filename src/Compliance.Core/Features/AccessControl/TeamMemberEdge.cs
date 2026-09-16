using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed record TeamMemberEdge(Uuid TeamId, Uuid MemberId);
