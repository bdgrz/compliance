namespace Bdgrz.Compliance.Features.Applications;

/// <summary>HTTP field presence, kept separate from the existing nullable scalar value.</summary>
public readonly record struct ApplicationRevisionField(bool IsSupplied);
