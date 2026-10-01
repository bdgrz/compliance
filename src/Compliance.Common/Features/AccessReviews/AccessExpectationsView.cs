using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>A system instance's expectations, expectation exceptions, and population exceptions.</summary>
public sealed record AccessExpectationsView(Uuid TenantId, Uuid SystemInstanceId, long Revision,
    IReadOnlyList<AccessExpectationView> Expectations,
    IReadOnlyList<AccessExpectationExceptionView> Exceptions,
    IReadOnlyList<AccessPopulationExceptionView> PopulationExceptions);
