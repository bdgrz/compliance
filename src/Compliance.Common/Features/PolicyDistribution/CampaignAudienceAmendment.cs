using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     An attributed change to the evidence population: <c>joiner</c>, <c>mover_in</c>,
///     <c>mover_out</c>, or <c>leaver</c>. Additions carry a due date; removals do not.
/// </summary>
public sealed record CampaignAudienceAmendment(Uuid PersonId, string DisplayName,
    string Reason, string? WorkerType, string? Department, DateOnly? DueOn);
