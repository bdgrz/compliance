namespace Bdgrz.Compliance.Features.Criteria;

static class CriteriaTextOverlayPolicy
{
    public static Criterion Apply(Criterion original, CriteriaTextOverlayRevision retained,
        CriteriaTextOverlayRevision current, bool export)
    {
        var allowed = new CriteriaOverlayUsageFlags(
            retained.Content.UsageFlags.Display && current.Content.UsageFlags.Display,
            retained.Content.UsageFlags.Export && current.Content.UsageFlags.Export);
        return original with
        {
            LicensedText = (export ? allowed.Export : allowed.Display) ? retained.Content.Text : null,
            Overlay = new CriteriaTextOverlayMetadata(retained.Revision, retained.Content.Supplier,
                retained.Content.LicenseReference, allowed, retained.Actor, retained.RecordedAt),
        };
    }
}
