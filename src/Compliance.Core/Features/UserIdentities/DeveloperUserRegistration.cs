using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Development-only identity settings used by the registration handler.</summary>
public sealed record DeveloperUserRegistration(bool Enabled)
{
    public const string Provider = "bdgrz-development";

    public static Uuid IdentifierNamespaceId { get; } =
        Uuid.Parse("8b53bb9d-09b9-5f2b-934c-2f04296d64ee", CultureInfo.InvariantCulture);
}
