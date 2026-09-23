using System.Globalization;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Configuration used once to seed the event-sourced operator roster.</summary>
public sealed class PlatformOperatorAuthority
{
    readonly Uuid[] _userIds;

    public PlatformOperatorAuthority(IEnumerable<Uuid> userIds, bool developerAuthentication = false)
    {
        _userIds = [.. userIds.Distinct()];
        DeveloperAuthentication = developerAuthentication;
    }

    public bool DeveloperAuthentication { get; }
    public IReadOnlyList<Uuid> BootstrapUserIds => _userIds;

    public static PlatformOperatorAuthority FromConfiguration(IConfiguration configuration,
        bool developerAuthentication)
    {
        var userIds = new List<Uuid>();
        foreach (var value in configuration.GetSection("PlatformOperators:UserIds").GetChildren())
        {
            if (!Uuid.TryParse(value.Value, CultureInfo.InvariantCulture, out var userId) || userId == Uuid.Empty)
                throw new InvalidOperationException("PlatformOperators:UserIds must contain nonempty UUIDs.");
            userIds.Add(userId);
        }

        return new PlatformOperatorAuthority(userIds, developerAuthentication);
    }
}
