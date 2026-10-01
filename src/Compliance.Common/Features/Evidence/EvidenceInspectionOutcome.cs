namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>The M0-D16 result of scanning an upload for malware and secrets before it becomes available.</summary>
public enum EvidenceInspectionOutcome
{
    Clean,
    Malware,
    SecretDetected,
    Invalid,
}
