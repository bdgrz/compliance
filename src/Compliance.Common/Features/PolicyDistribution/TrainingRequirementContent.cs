namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     A security-awareness training requirement (M0-D12). <c>AudienceKind</c> uses the policy
///     audience vocabulary; <c>DeliverySource</c> is <c>manual</c> or <c>lms_export</c>.
///     Compliance records completions and never delivers the course.
/// </summary>
public sealed record TrainingRequirementContent(string CourseName, string? Description,
    string AudienceKind, IReadOnlyList<string>? AudienceTeams, string DeliverySource);
