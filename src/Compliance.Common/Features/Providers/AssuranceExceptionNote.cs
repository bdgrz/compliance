namespace Bdgrz.Compliance.Features.Providers;

/// <summary>An exception noted by the issuer or reviewer, retained in the author's words.</summary>
public sealed record AssuranceExceptionNote(string? Reference, string Description);
