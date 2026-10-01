using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Why a variance item has its category, with the expectation or exception that explains it.</summary>
public sealed record AccessVarianceFinding(string Kind, Uuid? ExpectationId, Uuid? ExceptionId,
    string Explanation);
