using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record FirmStaffDirectoryView(long Sequence, IReadOnlyList<FirmStaffMemberView> Staff);
