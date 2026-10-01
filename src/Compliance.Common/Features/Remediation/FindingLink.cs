namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     A related record: control, criterion, evidence, access_decision, risk, vendor,
///     readiness_gap, or control_occurrence. Reference is the record ID or stable identifier.
/// </summary>
public sealed record FindingLink(string Kind, string Reference);
