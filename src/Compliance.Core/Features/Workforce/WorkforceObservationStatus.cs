namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Derives an observation's status from its closure: open until resolved or dismissed.</summary>
public static class WorkforceObservationStatus
{
    public static string Of(WorkforceObservationResolutionView? resolution) =>
        resolution?.Resolution ?? "open";
}
