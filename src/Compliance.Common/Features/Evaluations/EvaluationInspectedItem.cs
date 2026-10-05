namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     One exact item an evaluation inspects: a boundary, commitment, risk, criterion, control,
///     policy, provider, evidence, generic record, or artifact, identified by its reference and
///     the exact version or content digest inspected.
/// </summary>
public sealed record EvaluationInspectedItem(string Kind, string Reference, string Version);
