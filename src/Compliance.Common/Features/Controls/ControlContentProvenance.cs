namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     Where a control's authored content came from. <c>Origin</c> is
///     <c>organization_authored</c>, <c>template</c> (adapted from a named template), or
///     <c>supplied</c> (provided by a named outside party, such as an advisor). A template or
///     supplied origin names its source; provenance never attributes the content to a platform
///     actor who did not act, and an approved version keeps the provenance it was approved with.
/// </summary>
public sealed record ControlContentProvenance(string Origin, string? SourceName = null,
    string? SourceReference = null, string? SourceVersion = null);
