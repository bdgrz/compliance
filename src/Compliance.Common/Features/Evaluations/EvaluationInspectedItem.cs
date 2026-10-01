namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     One exact item an evaluation inspects: a governed <c>record</c> or an <c>artifact</c>,
///     identified by its reference and the exact version or content digest inspected.
/// </summary>
public sealed record EvaluationInspectedItem(string Kind, string Reference, string Version);
