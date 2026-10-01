using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     The exact material a campaign distributes: an approved <c>policy</c> version or a
///     <c>training</c> requirement version, identified by its content hash.
/// </summary>
public sealed record CampaignSubject(string Kind, Uuid RecordId, string Identifier,
    string Title, long Version, string ContentSha256);
