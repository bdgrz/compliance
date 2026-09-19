using System.Globalization;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Explicit platform operators. Development identities can provision local test tenants.</summary>
public sealed class PlatformOperatorAuthority
{
    readonly HashSet<Uuid> _userIds;

    public PlatformOperatorAuthority(IEnumerable<Uuid> userIds, bool developerAuthentication = false)
    {
        _userIds = [.. userIds];
        DeveloperAuthentication = developerAuthentication;
    }

    public bool DeveloperAuthentication { get; }

    public bool IsOperator(Uuid userId) =>
        userId != Uuid.Empty && (DeveloperAuthentication || _userIds.Contains(userId));

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
