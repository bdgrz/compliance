using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>Derives the stable identity of a control's exact version line.</summary>
public static class ControlVersionIds
{
    /// <summary>The version identity shared by the initial draft and its approved version.</summary>
    public static Uuid Initial(Uuid controlId) => Sequence(controlId, 1);

    /// <summary>The version identity of the control's n-th version line (1-based).</summary>
    public static Uuid Sequence(Uuid controlId, int sequence)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);
        return Uuid.CreateVersion5(controlId, "control-version-" +
            sequence.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>The decision target of a proposal to retire one exact approved version.</summary>
    public static Uuid Retirement(Uuid controlId, Uuid approvedVersionId) =>
        Uuid.CreateVersion5(controlId, "control-retirement-" + approvedVersionId);
}
