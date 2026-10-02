using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     An immutable review. The observed provider and report revisions are server captured and
///     historical; a later change never rewrites them. <c>Redacted</c> withholds a confidential or
///     restricted evidence citation only: the conclusion stays shareable.
/// </summary>
public sealed record ProviderReviewView(Uuid TenantId, Uuid ReviewId, Uuid ProviderId,
    ProviderReviewContent Content, long ProviderRevision, long? AssuranceReportRevision,
    ActorReference ReviewedBy, DateTimeOffset RecordedAt, bool Redacted = false);
