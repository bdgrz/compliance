namespace Bdgrz.Compliance.Features.Criteria;

static class CriteriaTextOverlayPurpose
{
    public const string Display = "display";
    public const string Export = "export";

    public static bool TryGetExport(string? purpose, out bool export)
    {
        export = purpose == Export;
        return purpose is null or Display or Export;
    }
}
