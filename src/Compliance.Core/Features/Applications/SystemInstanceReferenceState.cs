namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Whether a governed reference may name a system instance now, later, or not at all.</summary>
public enum SystemInstanceReferenceState
{
    Missing,
    /// <summary>The source exists but the tenant instance projection has not reached it.</summary>
    Pending,
    Declared,
}
