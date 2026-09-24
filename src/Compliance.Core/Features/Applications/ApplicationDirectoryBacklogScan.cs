using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>The first matching pending event, or whether the bounded scan stopped early.</summary>
public readonly record struct ApplicationDirectoryBacklogScan(DomainEvent? Match, bool Exceeded)
{
    /// <summary>The projection may already hold every matching event.</summary>
    public bool Clear => Match is null && !Exceeded;
}
