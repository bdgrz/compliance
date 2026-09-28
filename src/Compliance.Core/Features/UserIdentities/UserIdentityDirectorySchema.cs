using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

static class UserIdentityDirectorySchema
{
    public static readonly KvDirectoryIndex<UserIdentityDirectoryEntry> ByUser = new(
        "by_user", 1, static identity => identity.IsRevoked
            ? []
            : [identity.UserId.ToString(), identity.UserIdentityId.ToString()]);

    public static readonly KvDirectory<UserIdentityDirectoryEntry, Uuid> Directory = new(
        "user-identities",
        ComplianceCoreJsonContext.Default.UserIdentityDirectoryEntry,
        static identity => identity.UserIdentityId,
        static identityId => [identityId.ToString()],
        [ByUser]);

    public static readonly KvDirectory<UserIdentityDirectoryRevision, string> Revisions = new(
        "user-identity-directory-revisions",
        ComplianceCoreJsonContext.Default.UserIdentityDirectoryRevision,
        static revision => revision.Id,
        static id => [id], []);
}
