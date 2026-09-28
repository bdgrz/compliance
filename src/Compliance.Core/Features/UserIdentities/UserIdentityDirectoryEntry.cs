using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed record UserIdentityDirectoryEntry(
    Uuid UserId,
    Uuid UserIdentityId,
    string Provider,
    bool IsRevoked);

public sealed record UserIdentityDirectoryRevision(string Id, long Revision);

public sealed record UserIdentityDirectoryPage(
    long Revision,
    Page<UserIdentityDirectoryEntry> Identities);
