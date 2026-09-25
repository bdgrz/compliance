namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Field classes known to the authorization model. Workforce classes are added when M0-D06 confirms
///     them; evidence classes follow M0-D16.
/// </summary>
public static class FieldClasses
{
    /// <summary>Evidence content held in quarantine; readable only by an Org Admin (M0-D16).</summary>
    public static FieldClass EvidenceQuarantinedContent { get; } = new("evidence.quarantined_content");
}
