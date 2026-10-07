namespace Bdgrz.Compliance.Features.Applications;

/// <summary>A durable omission proposal, never authority to retire or accept. Impact and effects remain required.</summary>
public sealed record ApplicationImportRetirementProposal(ApplicationImportRetirementProposalStarted Start,
    IReadOnlyList<ApplicationImportRetirementRow> Rows, long Revision, string ProposalSha256);
