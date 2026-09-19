using System.Text.RegularExpressions;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class ReservedTenantRouteTests
{
    [Fact]
    public void EveryTopLevelServerAndClientRouteMustBeReserved()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Compliance.slnx")))
            root = root.Parent;
        Assert.NotNull(root);

        var server = File.ReadAllText(Path.Combine(root.FullName, "src/Compliance.App/Program.cs"));
        var client = File.ReadAllText(Path.Combine(root.FullName,
            "src/Compliance.App/ClientApp/src/pages/_routes.ts"));
        var serverSegments = Regex.Matches(server, "\"/([a-z][a-z0-9-]*)(?:/|\")")
            .Select(match => match.Groups[1].Value);
        var clientSegments = Regex.Matches(client, "route\\('/([a-z][a-z0-9-]*)(?:/|')")
            .Select(match => match.Groups[1].Value);

        foreach (var segment in serverSegments.Concat(clientSegments).Distinct(StringComparer.Ordinal))
            Assert.True(TenantSlugs.IsReservedRoute(segment),
                $"Top-level route '{segment}' is missing from the tenant slug reserved-route registry.");
    }
}
