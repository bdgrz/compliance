namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>M0-D16 artifact states and the reasons attached to quarantine and rejection.</summary>
public static class EvidenceArtifactStates
{
    public const string PendingInspection = "pending_inspection";
    public const string Available = "available";
    public const string Quarantined = "quarantined";
    public const string Rejected = "rejected";
    public const string Disposed = "disposed";

    public const string Malware = "malware";
    public const string SecretDetected = "secret_detected";
    public const string Invalid = "invalid";
}
